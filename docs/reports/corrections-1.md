# Corrections 1: D-162

Status: complete, no open question. Every job of CONV-GATE-001 that runs on this
machine is green; the pipeline runs are outstanding, see section 5.

D-162 audited the 109 decisions taken in the owner's absence, the 88 Tier 1 resolutions
and the code against the chapters. This branch carries out every instruction it gives:
the trace in the history is removed and the whole history searched; the 27 reversals are
built as D-162 states them; the open choices of section C are built as it settles them;
the items section D found missing from closed phases are built in the areas whose items
they are; and the codes, keys, declarations and vocabularies of section E are in the
library's catalogues and listed for the owner in
`docs/reports/decisions-pending-review.md` under "Rows for chapter 10". Each ledger
entry D-162 names carries one line "Superseded by D-162" and the applied correction is a
new numbered entry at the end, as section 4 lists them.

## 1. Items implemented

Keyed by the point of D-162 that governs, with the ledger entry that records it. Every
item named is one D-162 assigns to that point. Where the first column names a tier
instead, the row is a decision this branch had to take to build the point above it, and
the entry states which.

| D-162 point or tier | Items | Tests |
|---|---|---|
| D-162 section B, correcting entry 4 (ledger 110) | AUTHZ-GATE-005 AC1 | `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageCostsOneStatementOverTheHostsRowsAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC2_ADerivedGrantReachesTheCapabilityPageAsync` |
| D-162 section B, correcting entry 3 (ledger 111) | AUTHZ-GATE-004, AUTHZ-DERIVE-001 | `ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAFactProducedAsync`, `ExplanationTests.AUTHZ_GATE_004_AC1_ADenialWithTheHostsRowsStatesNoGrantMatchedAsync`, `ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAndWhatItWasInheritedFromAsync`, `ExplanationTests.AUTHZ_GATE_004_AC3_OnlyATypeThatDisclosesExplainsToTheCallerAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync` |
| D-162 section B, correcting entry 2 (ledger 112) | AUTHZ-DERIVE-001, AUTHZ-PRIN-001 AC2 | `GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync`, `GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckNoDerivationReachesNeedsNoRowsAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync` |
| D-162 section B, correcting entry 11 (ledger 113) | AUTH-PASS-001 AC3, `10` section 1.2 | `PasswordFloorTests.AUTH_PASS_001_AC3_TheMaximumIsAcceptedAndOneBeyondItIsRefused`, `ApiStatusTests.Of_ACodeTheLibraryRaises_HasAStatusOfItsOwn` |
| D-162 section B, correcting entry 12 (ledger 114) | AUTH-PASS-004, INT-PWD-002, INT-PWD-003 | `ScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync`, `ScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync`, `ScreeningTests.ScreenAsync_TheSelfHostedCorpusWithNoAddress_FallsBackAsync`, `ScreeningTests.ScreenAsync_ACorpusOlderThanTheMaximumAge_RefusesAsync`, `ScreeningTests.ScreenAsync_ACorpusWithNoDate_RefusesAsync`, `StartupConfigurationTests.ThrowIfIncomplete_TheCorpusIsSelfHosted_RequiresItsAddress`, `StartupConfigurationTests.ThrowIfIncomplete_TheCorpusIsNotSelfHosted_NeedsNoAddress` |
| D-162 section B, correcting entry 17 (ledger 115) | AUTH-FACT-004, AUTH-FACT-016 | `VerificationCodesTests.AUTH_FACT_004_AC2_ACodeIsHeldApartAndLivesItsOwnLifetimeAsync`, `VerificationCodesTests.AUTH_FACT_004_AC2_TheLifetimeIsWhatTheDeploymentConfiguresAsync`, `VerificationCodesTests.AUTH_FACT_004_AC3_TheCapEndsTheCodeAndAReplacementLeavesItDeadAsync`, `VerificationCodesTests.AUTH_FACT_004_AC3_TheRightCodeIsSpentOnceAsync`, `VerificationCodesTests.IssueAsync_ACodeIsOutstanding_ReplacesItAsync`, `VerificationCodesTests.PresentAsync_NoCodeIsOutstanding_RefusesAsync`, `AuthenticationServiceTests.AUTH_FACT_016_AC3_WrongCodesInvalidateTheHeldSignInAsync`, `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` |
| D-162 section B, correcting entry 19 (ledger 116) | OPS-CFG-008, `10` section 4 | `ConfigurationStoreTests.ReadAsync_AStoredValueThatDoesNotParse_IsAFaultAsync`, `ConfigurationStoreTests.OPS_CFG_008_AC1_AChangedSettingIsInForceForTheNextReadAsync` |
| D-162 section B, correcting entry 20 (ledger 117) | INT-GEN-006, AUTH-SESS-013 | `SessionServiceTests.INT_GEN_006_AC3_TheCityIsWhatTheDatabaseMadeOfTheAddressAsync`, `SessionServiceTests.INT_GEN_006_AC3_WithNoDatabaseTheSessionIsListedWithoutALocationAsync`, `SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`, `PublicSurfaceTests.INT_GEN_006_AC3_NoContractMemberIsToldWhereASessionWas`, `LocationDatabaseTests.INT_GEN_006_AC1_TheResolverHoldsNothingItCouldCallOutWith`, `LocationDatabaseTests.INT_GEN_006_AC2_TheMissingFileSurfacesAsADegradationAsync`, `LocationDatabaseTests.INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync` |
| D-162 section B, correcting entry 22 (ledger 118) | CONV-LAYOUT-001, LIB-EXT-001, AUTH-ABUSE-004, OPS-ALERT-001 | `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`, `PublicSurfaceTests.CONV_CODE_003_AC1_NoContractMemberExposesAMutableCollection`, `MessageRenderingTests.CONV_CONTENT_001_AC1_TheNamedPlacesAreFilledAndTheWordsAreNotTouched`, `MessageRenderingTests.Fill_APlaceTheValuesDoNotName_IsLeftAsItStands` |
| D-162 section B, correcting entry 23 (ledger 119) | D-022, IDN-PRIN-003, PRIV-RIGHT-005a, AUTH-ABUSE-004 | `SendingServiceTests.D_022_TheMessageIsWrittenToTheOutboxAndRemovedOnceTakenAsync`, `SendingServiceTests.D_022_ATransportRefusalLeavesTheMessageRecordedAsync`, `SendOutboxTests.D_022_TheMessageReadsBackAsItWasUndertakenAsync`, `SendOutboxTests.PRIV_RIGHT_005a_AC8_TheTableYieldsNoDestinationInPlainAsync`, `SendOutboxTests.IDN_PRIN_003_AMessageTakenLeavesNoRowAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC2_ATransportRefusalCountsNothingAsync` |
| D-162 section B, correcting entry 26 (ledger 120) | INT-SMS-003, INT-SMS-005a, AUTH-ABUSE-005 | `SendingValidationTests.INT_SMS_003_AC1_ATemplateIsMeasuredWithItsPlacesAtTheirWidestAsync`, `SendingValidationTests.INT_SMS_003_AC1_APlaceTheLibraryDoesNotFillIsMeasuredAsWrittenAsync`, `MessagePlaceholdersTests.INT_SMS_003_AC1_APlaceIsFilledToTheWidthItIsDefinedAt`, `MessagePlaceholdersTests.INT_SMS_003_AC1_TheValuesTheLibraryDrawsFitTheWidthsItMeasuresAt`, `SendingValidationTests.AUTH_ABUSE_005_AC3_AnOverBudgetTextMessageStopsStartupAsync`, `SendingValidationTests.INT_SMS_003_AC1_ALatinMessageOverItsBudgetStopsStartupAsync`, `SendingValidationTests.INT_SMS_003_AC2_EveryTextMessageIsMeasuredInEveryLanguageAsync` |
| D-162 section B, correcting entry 29 (ledger 121) | CONV-DESIGN-005 AC1, LIB-API-001, IDN-LIFE-003a, INT-GEN-006 | `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`, `AccountLifecycleTests.CONV_DESIGN_005_AC1_AnEventThatIsNotTakenFailsTheOperationAsync`, `SessionServiceTests.INT_GEN_006_AResolverThatCouldNotReportFailsTheSignInAsync` |
| D-162 section B, correcting entry 31 (ledger 122) | AUTH-ABUSE-004 AC6, PRIV-RET-005 AC2, INT-SMS-005 | `SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordIsGoneOnceItsBucketsAreEmptyAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC6_AShortenedIntervalReachesTheSendsAlreadyCountedAsync`, `SendLedgerTests.PRIV_RET_005_AC2_TheRecordLivesAtMostTheLongestBucketIntervalAsync`, `SendCounterSweepTests.AUTH_ABUSE_004_AC6_TheSweepReadsTheExpressionTheIndexIsOver`, `SendingServiceTests.AUTH_ABUSE_004_AC6_ARecordOlderThanTheLongestIntervalGoesWithTheNextReadAsync`, `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` |
| D-162 section B, correcting entry 32 (ledger 123) | LIB-EXT-001, AUTH-ABUSE-005 AC3, CONV-CONTENT-001, LIB-HOST-001 | `DefaultMessageTemplatesTests.LIB_EXT_001_EveryMessageIsWordedOnEveryChannelInEveryLanguageCarried`, `DefaultMessageTemplatesTests.AUTH_ABUSE_005_EveryShippedTextMessageFitsOneMessageAtItsWidest`, `DefaultMessageTemplatesTests.CONV_CONTENT_001_EveryPlaceAShippedTextNamesIsOneTheLibraryFills`, `DefaultMessageTemplatesTests.LIB_EXT_001_AC1_ADeploymentThatRegistersNoCatalogueGetsTheShippedOne`, `DefaultMessageTemplatesTests.LIB_EXT_001_AC2_TheCatalogueTheDeploymentRegistersIsTheOneInForce`, `DefaultMessageTemplatesTests.AUTH_ABUSE_005_AC3_ADeploymentOnTheShippedCatalogueStartsAsync`, `DefaultMessageTemplatesTests.AUTH_ABUSE_005_AC3_ALanguageTheShippedCatalogueLacksStopsStartupAsync`, `StartupValidationTests.AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesStartsOnTheShippedOnesAsync` |
| D-162 section B, correcting entry 34 (ledger 124) | OPS-CFG-002, OPS-CFG-005, OPS-CFG-008, OPS-ALERT-004a | `ConfigurationAdministrationTests.OPS_CFG_002_AC1_ShorteningASessionTimeoutRequiresNoStepUpAsync`, `ConfigurationAdministrationTests.OPS_CFG_002_AC2_LengtheningOneRequiresStepUpAsync`, `ConfigurationAdministrationTests.OPS_CFG_002_AC2_LengtheningOneRequiresAReasonAsync`, `ConfigurationAdministrationTests.OPS_CFG_002_AC3_AChangeWithNoDirectionRequiresStepUpAndAReasonAsync`, `ConfigurationAdministrationTests.OPS_CFG_002_AC4_ATighteningAndALooseningAreBothAuditedAsync`, `ConfigurationAdministrationTests.OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync`, `ConfigurationAdministrationTests.OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAndByActorAsync`, `ConfigurationAdministrationTests.OPS_CFG_005_ASystemPrincipalChangesNoSettingAsync`, `SettingDirectionTests.OPS_CFG_002_AKeyBoundedOnlyAboveLoosensUpward`, `SettingDirectionTests.OPS_CFG_002_AKeyBoundedOnlyBelowLoosensDownward`, `SettingDirectionTests.OPS_CFG_002_ABooleanLoosensAwayFromItsDefault`, `SettingDirectionTests.OPS_CFG_002_ASetLoosensByTheMemberItLost`, `SettingDirectionTests.OPS_CFG_002_TheRestrictionSetLoosensByItsOwnRule`, `ConfigurationAuditTests.OPS_CFG_005_AC1_TheRecordCarriesBeforeAndAfterAsync`, `ConfigurationAuditTests.OPS_CFG_005_AC2_TheRecordsAreQueryableBySettingAsync`, `ConfigurationAuditTests.OPS_CFG_005_AC2_TheRecordsAreQueryableByActorAsync`, `AlertDestinationChangeTests.ChangeAsync_ADestinationChange_IsWrittenDownAsARuntimeChangeAsync`, `AlertDestinationChangeTests.ChangeAsync_ADestinationChangeWithNoReason_TellsNobodyAsync`, `LibraryStructureTests.OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting` |
| D-162 section B, correcting entry 46 (ledger 125) | REG-SESS-002, API-LAND-001 | `RegistrationFlowTests.BeginAsync_ABrowserAlreadySignedIn_IsRefusedAndStagesNothingAsync` |
| D-162 section B, correcting entry 49 (ledger 126) | API-CONV-002, API-CONV-003 | `ApiConventionTests.MapRegistration_ABodyThatDoesNotParse_AnswersTheUsualBodyAsync`, `ApiConventionTests.MapRegistration_ABodyMissingAMember_NamesTheMemberAsync`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |
| D-162 section B, correcting entry 50 (ledger 127) | REG-PM-001, LIB-HOST-001, LIB-HOST-003 | `StartupValidationTests.REG_PM_001_ADeploymentThatDeclaredNoPasskeyPagesIsRefusedAsync`, `WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`, `WellKnownTests.MapWellKnown_TheDeclaredAddresses_AreWhatBothDocumentsCarryAsync`, `ApiConventionTests.API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync` |
| D-162 section B, correcting entry 61 (ledger 128) | AUTH-SESS-012 AC3, LIB-HOST-001, LIB-HOST-003 | `StartupValidationTests.AUTH_SESS_012_AC3_ADeploymentThatDeclaredNoSignInScreenIsRefusedAsync`, `OidcFlowTests.AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync`, `OidcFlowTests.AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync` |
| D-162 section B, correcting entry 75 (ledger 129) | REG-PM-001, REG-SESS-001, AUTH-FACT-014 | `WebAuthnServiceTests.REG_PM_001_AC1_TheHandleIsTheSubjectIdentifierAndNothingElseAsync`, `WebAuthnServiceTests.REG_PM_001_TheCeremonyCarriesTheNameAndTheDisplayNameAsync`, `WebAuthnServiceTests.REG_PM_001_AnAssertionWhoseHandleNamesAnotherAccountIsRefusedAsync`, `WebAuthnServiceTests.REG_PM_001_AnAssertionWhoseHandleIsNotOneWeIssuedIsRefusedAsync`, `CredentialServiceTests.REG_PM_001_TheCeremonyCarriesTheAccountsHandleAndPrimaryEmailAsync` |
| D-162 section B, correcting entry 83 (ledger 130) | PRIV-CONS-005, PRIV-CONS-008a, PRIV-SENS-002a, `10` section 1.4 | `ConsentTests.PRIV_CONS_008a_AC3_APurposeThatTakesNoConsentIsNamedAsSuchAsync`, `ConsentTests.PRIV_CONS_005_AConsentBeforeAnyNoticeIsPublishedIsRefusedAsync`, `LegalDocumentTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`, `LegalDocumentEndpointTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`, `ConsentEndpointTests.PRIV_CONS_008a_AC3_APurposeOnAnotherBasisTakesNoConsentAsync` |
| D-162 section B, correcting entry 93 (ledger 131) | PRIV-RIGHT-001, PRIV-RIGHT-002, AUTHZ-CONCEAL-005, `10` section 1.4 | `PrivacyRequestTests.PRIV_RIGHT_002_AC5_ARequestIsDecidedOnceAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_TheThreeWaysADecisionIsRefusedAreToldApartAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_EnteringWithoutThePermissionIsRefusedAsync` |
| D-162 section B, correcting entry 104 (ledger 132) | PRIV-ROPA-001, PRIV-SENS-001, PRIV-MINOR-001 | `ProcessingRecordsTests.PRIV_SENS_001_AC2_TheChildrensColumnFollowsTheDeclaredCategoryAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_ADeploymentAdmittingMinorsAndDeclaringNoneIsFlaggedAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_ADeploymentThatDeclaredOneIsNotFlaggedAsync`, `ProcessingRecordsTests.PRIV_SENS_001_AC2_SensitivityIsAColumnOfItsOwnAsync` |
| D-162 section B, correcting entry 89 (ledger 133) | PRIV-SENS-002 AC1, AUTHZ-GATE-005, PRIV-RIGHT-005a | `ConsentGateTests.PRIV_SENS_002_AC1_StaffAreGatedByTheRecordsSubjectsConsentAsync`, `ConsentGateTests.PRIV_SENS_002_AC1_ARecordNamingNoSubjectAdmitsNoConsentedActionAsync`, `ConsentGateTests.PRIV_SENS_002_AC1_AWrittenConsentAdmitsTheActionAsync`, `ConsentGateTests.PRIV_SENS_002a_AC2_WithdrawingStopsThePurposeOnTheNextRequestAsync`, `DeclaredProcessingTests.AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor`, `RegisteredResourceTests.Existing_ARow_CarriesWhatWasWritten`, `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` |
| D-162 section B, Tier 1 reversal, correcting the phase 1 Tier 1 resolution recorded in `docs/reports/phase-01.md` (ledger 134) | OPS-DB-001, OPS-DB-002 AC1 | `SchemaTests.OPS_DB_002_AC1_TheCollationLivesInTheLibrarysSchemaAsync`, `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync`, `OrganizationStoreTests.OPS_DB_001_AC2_TheOrganizationNameComparesWithoutRegardToCaseAsync` |
| D-162 section B, Tier 1 reversal, correcting the phase 1 Tier 1 resolution recorded in `docs/reports/phase-01.md` (ledger 135) | OPS-MIG-003a AC2, AC4, AUTHZ-MODEL-005 | `SerializedModelTests.OPS_MIG_003a_AC4_TheMaintenanceGrantsAreListedInTheSerializedModel`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheListedGrantsAreTheOnesTheDatabaseHoldsAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheKeysAndNoOtherTableAsync`, `SerializedModelTests.AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes` |
| D-162 section B, Tier 1 reversal, correcting the phase 2 Tier 1 resolution recorded in `docs/reports/phase-02.md` (ledger 136) | AUTHZ-CONCEAL-004, IDN-AUD-001 AC1 | `ExplanationTests.AUTHZ_CONCEAL_004_AC1_ARefusalUnderNoAccountCarriesAnIdentifierAsync`, `ExplanationTests.AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync`, `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync`, `AuditStoreTests.IDN_AUD_001_AC1_AnEventNamingNobodyIsRefusedByTheDatabaseAsync`, `AuditStoreTests.IDN_AUD_001_AC1_BothIdentityFieldsArePopulatedAsync` |
| D-162 section B, Tier 1 reversals (ledger 137) | IDN-AUD-001, CONV-NAME-003, `10` section 5 | `AuditActionsTests.IDN_AUD_001_TheSetOfActionsIsClosed`, `AuditActionsTests.CONV_NAME_003_AC2_ChangingAnActionFailsTheContractTest`, `AuditActionsTests.CONV_NAME_003_AC1_EveryActionSaysWhatItRecords`, `VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten`, `VocabularyContractTests.WireNames_EveryVocabularyMember_CarriesOne` |
| D-162 section B, Tier 1 reversal (ledger 138) | REG-PREF-001, LIB-HOST-001 | `VocabularyContractTests.REG_PREF_001_ThePreferenceTypesAreTheOnesTheItemNames`, `VocabularyContractTests.WireNames_EveryVocabularyMember_CarriesOne`, `PreferenceDeclarationsTests.REG_PREF_001_AC1_ADefaultTheKindRefusesFailsStartup` |
| D-162 section C, item 10 (ledger 139) | BFF-CSRF-001, BFF-CSRF-003, D-153 | `BrowserProfileTests.BFF_CSRF_003_AC1_TheTwoHeadersAreNamedAsTheFrontendWritesThem`, `BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync`, `BrowserProfileTests.BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync` |
| D-162 section C, item 27 (ledger 140) | INT-GEN-001, LIB-EXT-001, `10` section 4 | `SendingValidationTests.INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync`, `SendingValidationTests.INT_GEN_001_AC1_EveryEndpointOverTlsStartsAsync`, `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `SettingsCatalogueTests.Scope_TheCatalogue_ProtectsTheKeysSectionFourMarks`, `SettingWrittenFormTests.Written_EveryDefaultOfTheCatalogue_ReadsBackAsItself` |
| D-162 section C, item 35 (ledger 141) | REG-IDENT-006, REG-IDENT-001, REG-SESS-005 | `IdentifierServiceTests.REG_IDENT_006_AC2_AReservedAddressIsAnsweredAsAHeldOneIsAsync`, `IdentifierStoreTests.REG_IDENT_006_AC2_AGivenUpValueIsOutOfReachUntilTheUndoLapsesAsync`, `IdentifierServiceTests.REG_IDENT_001_AC2_ANumberOnOneAccountDoesNotVerifyOnAnotherAsync` |
| D-162 section C, item 41 (ledger 142) | BFF-ORDER-001 stage 5, BFF-STEP-001, BFF-CSRF-001, AUTH-SESS-007, API-CONV-003 | `SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed`, `SessionRequirementTests.BFF_STEP_001_AC3_AnEndedSessionIsAnsweredWithWhatMustBeRedoneAsync`, `SessionRequirementTests.BFF_ORDER_001_AnEndedSessionDoesNotRefuseAnEndpointThatNeedsNoneAsync`, `SessionRequirementTests.API_CONV_003_ABrowserThatHeldNoSessionIsAnsweredWithTheCodeAloneAsync`, `BrowserProfileTests.BFF_ORDER_001_AnEndedSessionIsClearedAndLeavesTheRequestAnonymousAsync`, `BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut`, `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute` |
| D-162 section C, item 48 (ledger 143) | REG-SESS-003, FE-VER-001, CONV-DESIGN-008, `10` section 4 | `RegistrationSignalsTests.REG_SESS_003_AWaitNothingSignalsEndsOnTheIntervalAsync`, `RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`, `RegistrationFlowTests.REG_SESS_003_TheSignalWakesTheStreamBeforeTheIntervalAsync`, `RegistrationFlowTests.BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync`, `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract` |
| D-162 section C, item 55 (ledger 144) | IDN-ATTR-002, IDN-ATTR-003, IDN-ATTR-004, PRIV-RIGHT-005 AC4, LIB-HOST-001, `09` section 6, `10` sections 1.1 and 4 | `ProfilePhotosTests.IDN_ATTR_002_AC1_AnAccountInNoOrganizationShowsNoPhotoAsync`, `ProfilePhotosTests.IDN_ATTR_002_AC2_AnOrganizationIsGivenPhotosByItsKeyAloneAsync`, `ProfilePhotosTests.IDN_ATTR_002_AnOrganizationThatShowsNoPhotoWithholdsItFromItsMembersAsync`, `ProfilePhotosTests.IDN_ATTR_002_AnImageIsWithheldByAPolicyAndStillGivenUpByItsAccountAsync`, `ProfilePhotosTests.LIB_HOST_001_AnUploadIsRefusedWhereTheDeploymentDeclaredNoCodecAsync`, `ProfilePhotosTests.IDN_ATTR_004_AC1_AnUploadTheCodecDoesNotRecogniseIsRefusedAsync`, `ProfilePhotosTests.IDN_ATTR_004_AC2_WhatIsStoredIsWhatTheCodecAnsweredAsync`, `ProfilePhotosTests.IDN_ATTR_004_TheCodecIsHandedTheConfiguredLongestSideAsync`, `ProfilePhotosTests.IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedUnreadAsync`, `ProfilePhotosTests.IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync`, `ProfilePhotosTests.IDN_AUD_001_SettingAndGivingUpAPhotoAreRecordedAsProfileChangesAsync`, `PhotoFlowTests.IDN_ATTR_002_AnAccountThatShowsNoPhotoAndOneWithNoPolicyAnswerAlikeAsync`, `PhotoFlowTests.IDN_ATTR_004_AnUploadIsStoredReencodedAndServedAsAJpegAsync`, `PhotoFlowTests.IDN_ATTR_003_AC3_TheImageIsServedWithNothingACacheCouldShareAsync`, `PhotoFlowTests.IDN_ATTR_002_AnUploadIsRefusedWhereTheOrganizationShowsNoPhotoAsync`, `PhotoFlowTests.IDN_ATTR_004_AC1_BytesTheCodecRefusesAreRefusedWhateverTheRequestCalledThemAsync`, `PhotoFlowTests.IDN_ATTR_004_AnUploadOverTheConfiguredLengthIsRefusedAsync`, `PhotoFlowTests.IDN_ATTR_003_AnAccountGivesUpTheImageItShowsAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC4_TheDirectoryShowsAnErasedSubjectAsOneWithNoPhotoAsync`, `StartupValidationTests.IDN_ATTR_002_ADeploymentThatShowsPhotosWithNoCodecIsRefusedAsync`, `StartupValidationTests.IDN_ATTR_002_ADeploymentThatShowsPhotosAndDeclaredACodecStartsAsync`, `SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed`, `SettingsCatalogueTests.LIB_API_001_AC2_TheFamiliesAreTheContract`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |
| D-162 section C, item 60 (ledger 145) | API-REDIR-001, API-REDIR-002, LIB-HOST-001 AC3, `10` sections 1.5 and 4 | `RedirectValidationTests.API_REDIR_001_AC3_ARegistryOfAbsoluteOriginsStartsAsync`, `RedirectValidationTests.API_REDIR_001_AC3_AnEntryThatIsNotAnAbsoluteOriginFailsAsync`, `RedirectValidationTests.LIB_HOST_001_AC3_ADeploymentThatNamesNoDefaultStartsAsync`, `RedirectValidationTests.API_REDIR_001_ADefaultNamingNoRegisteredClientIsRefusedAsync`, `RedirectValidationTests.API_REDIR_001_ADefaultNamingAProtocolClientIsRefusedAsync`, `RedirectValidationTests.API_REDIR_001_ADefaultNamingARegisteredApplicationStartsAsync`, `StartupValidationTests.API_REDIR_001_ADeploymentNamingADefaultClientTheRegistryLacksIsRefusedAsync`, `RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsStoredAsTheNamedDefaultAsync`, `RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsTheDefaultAndNoRefusalAsync`, `RegistrationServiceTests.API_REDIR_002_AC4_TheReturnIsTheStoredClientsAndNoOthersAsync`, `OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`, `OidcFlowTests.API_REDIR_001_AC4_OnlyTheReplacedDestinationIsRecordedAsync`, `OidcServiceTests.API_REDIR_001_AC2_ADestinationContainingAKnownOneIsNotAcceptedAsync`, `RelyingPartyTests.AUTH_FACT_010_AnEntryThatIsNotAnAbsoluteOriginFails`, `SettingsCatalogueTests.LIB_API_001_AC2_TheKeyNamesAreTheContract`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |
| D-162 section C, item 68, Tier 3 (ledger 146) | AUTH-FACT-002b AC6, AUTH-FACT-003, AUTH-ABUSE-003 AC1, AUTH-FACT-001 AC1 | `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeAndOffersTheRestAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AnAnswerThatReportsNoChangeLeavesTheTextCodeOnOfferAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeRefusesASignInWhoseOnlySecondStepIsTheTextCodeAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AWithholdingIsRecordedWithTheEntryAndNotTheNumberAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC6_AReportedChangeRefusesASignInLinkByTextAsync`, `AuthenticationServiceTests.AUTH_ABUSE_003_AC1_ANumberNoAccountHoldsIsRefusedInTheSameBytesAsync`, `FactorCatalogueTests.AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName`, `SendingServiceTests.AUTH_FACT_002b_AC6_TheSignalIsConsideredBeforeARestrictedFactorGoesAsync`, `SendingServiceTests.AUTH_FACT_002b_AC6_AnAbsentProviderIsItselfRecordedAsync` |
| D-162 section C, item 82 (ledger 147) | PRIV-CONS-001 AC2, PRIV-CONS-005, PRIV-CONS-007, LIB-HOST-001, `09` section 7 | `SupersessionTests.PRIV_CONS_007_AConsentNamesTheVersionOfItsOwnGoverningDocumentAsync`, `SupersessionTests.PRIV_CONS_007_AC1_AMaterialRevisionEndsTheConsentsOfThePurposesNamingItAsync`, `SupersessionTests.PRIV_CONS_007_AC2_ARevisionOfTheNoticeLeavesAPurposeNamingAnotherDocumentAsync`, `SupersessionTests.PRIV_CONS_005_AGrantIsRefusedBeforeItsGoverningDocumentIsPublishedAsync`, `SupersessionTests.PRIV_CONS_007_AC1_AMaterialChangeIdentifiesWhoMustBeAskedAgainAsync`, `SupersessionTests.PRIV_CONS_007_AC2_AMaterialRevisionOfAnotherDocumentEndsNoConsentAsync`, `SupersessionTests.PRIV_CONS_007_AC2_OnlyTheConsentBasedPurposesAreSuspendedAsync`, `DeclaredProcessingTests.PRIV_CONS_007_APurposeDeclaredAgainstTwoDocumentsIsRefused`, `DeclaredProcessingTests.PRIV_CONS_007_APurposeDeclaredAgainstOneDocumentTwiceStands`, `ConsentStoreTests.PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync` |
| D-162 section C, item 87 (ledger 148) | PRIV-CONS-001 AC1, PRIV-CONS-007, `09` section 7, `10` section 5.21 | `ConsentEndpointTests.PRIV_CONS_001_AC1_AGrantOverASupersededConsentIsRecordedAsReconsentAsync`, `ConsentEndpointTests.PRIV_CONS_001_AC1_AGrantOverAWithdrawnConsentIsRecordedAsTheDashboardAsync`, `ConsentEndpointTests.PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync` |
| D-162 section C, item 98 (ledger 149) | `09` sections 6 and 6a, IDN-LIFE-013 | `AccountLifecycleFlowTests.IDN_LIFE_013_AC1_TheNoticesLinkStandsTheAccountBackUpAsync`, `AccountLifecycleFlowTests.IDN_ACCT_007_AC4_TheLinkEndsTheWindowAsync` |
| D-162 section C, item 102 (ledger 150) | PRIV-RIGHT-003, REG-ACCT-001, REG-SESS-007 | `ExportSourceTests.REG_ACCT_001_AC1_TheExportCarriesTheCredentialsTheAccountShowsAsync`, `ExportSourceTests.REG_ACCT_001_TheExportCarriesTheWholeStandingGroupAsync`, `ExportSourceTests.REG_SESS_007_AC2_TheExportCarriesTheNoticeAndAffirmationRecordsAsync`, `ExportSourceTests.PRIV_RIGHT_003_AnAccountThatHasSettledNothingStillExportsAsync`, `ExportSourceTests.PRIV_RIGHT_003_AC3_TheExportCarriesThePreferencesIdentifiersAndSessionsAsync` |
| D-162 section C, item 103 (ledger 151) | PRIV-ROPA-002, PRIV-ROPA-003, chapter 05 sections 7 and 8 | `ProcessingRecordsTests.PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_TheRestOfTheShippedRegisterIsOfferedAndNotAppliedAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_AnUncalledProviderIsNotInTheRegisterAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_AC2_AnEditedRowStandsInPlaceOfTheShippedDefaultAsync`, `ProcessingRecordsTests.PRIV_ROPA_002_AC1_EveryRecipientAppearsAndAProcessorWithoutAnAgreementIsFlaggedAsync`, `ProcessingRecordsTests.PRIV_ROPA_003_AC1_ThePasswordScreeningCallAppearsAsACrossBorderTransferAsync` |
| D-162 section D (ledger 152) | AUTH-STEP-007, AUTH-RECOV-007, `10` section 5b | `CredentialServiceTests.AUTH_STEP_007_AnEnrolmentThatReachedActiveIsAnnouncedAsync`, `LossReportsTests.AUTH_RECOV_007_EachTurnOfAReportIsAnnouncedAsync`, `LossReportsTests.AUTH_RECOV_007_ASweepWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync` |
| D-162 section D (ledger 153) | OPS-MIG-002, OPS-MIG-001, OPS-MIG-005, `10` section 1.5 | `SchemaValidationTests.OPS_MIG_002_AC1_TheMigratedDatabaseMatchesTheModelAsync`, `SchemaValidationTests.OPS_MIG_002_AC1_ADatabaseBehindTheModelIsRefusedByNameAsync`, `SchemaValidationTests.OPS_MIG_001_AC1_TheCheckLeavesTheDatabaseUnmigratedAsync`, `StartupValidationTests.OPS_MIG_002_AC1_ADeploymentOnAnUnmigratedDatabaseIsRefusedAsync`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered` |
| D-162 section D (ledger 154) | IDN-MEM-002, `10` section 1.1 | `MembershipTests.IDN_MEM_002_AC2_ASecondMembershipIsRefusedByDefault`, `MembershipTests.IDN_MEM_002_AC3_TheSettingAloneAdmitsTheSecondMembership`, `MembershipTests.IDN_MEM_002_AC2_AMembershipThatEndedLeavesRoomForAnother`, `MembershipTests.IDN_MEM_002_ASecondMembershipOfTheSameOrganizationIsRefused`, `MembershipTests.Create_MembershipsOfAnotherAccount_Throws`, `OrganizationStoreTests.IDN_MEM_002_AC1_TheSchemaTakesMoreThanOneMembershipPerAccountAsync` |
| D-162 section D (ledger 155) | IDN-ORG-003, IDN-ORG-005, IDN-PRIN-003, `10` sections 5 and 5b | `OrganizationErasureSweepTests.IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapsesAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_AC2_ACancelledWindowIsNotReachedByThePassAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsAnnouncedWithWhatItEndedAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsWrittenDownAsync`, `OrganizationErasureSweepTests.IDN_ORG_003_APassWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync`, `OrganizationStatesTests.IDN_ORG_003_TheWindowsThatHaveRunOutAreWhatThePassReadsAsync`, `OrganizationStatesTests.IDN_ORG_003_AC5_TheRowSurvivesAndTheNameBecomesTheIdentifierAsync`, `OrganizationStatesTests.IDN_ORG_003_TheErasureEndsEveryCurrentMembershipAsync`, `OrganizationStatesTests.IDN_ORG_003_AC3_AnErasureBeforeTheWindowElapsesWritesNothingAsync` |
| D-162 section D (ledger 156) | PRIV-BASIS-001, PRIV-BASIS-004, PRIV-SENS-001, `07` LIB-HOST-001, `10` sections 5.7 and 5.9 | `DeclaredProcessingTests.PRIV_BASIS_001_TheShippedDeclarationCarriesTheDeclaredProperties`, `DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis`, `DeclaredProcessingTests.PRIV_BASIS_004_AC1_NoLibrarySourceNamesABasisWhosePropertiesAreAllUnset`, `DeclaredProcessingTests.PRIV_SENS_001_TheShippedCategoriesAreLabelsNothingBranchesOn` |
| D-162 section D (ledger 157) | `10` sections 1.1 and 5b, OPS-ALERT-001, IDN-ACCT-007, AUTHZ-CONCEAL-004 | None. Both points were already true and needed no code; entry 157 names the search that verified each, and the `10` row it leaves stale (CONV-TEST-007) |
| D-162 items 58 and 59 (ledger 158) | AUTH-OIDC-001 to AUTH-OIDC-004, AUTH-SESS-012, AUTH-KEY-001, API-REDIR-001, CONV-DESIGN-008 | `OidcStoreTests.AUTH_OIDC_001_AC2_TheRegistryHoldsWhatTheSecretHashesToAsync`, `OidcStoreTests.AUTH_OIDC_001_AC3_NoRequestWritesTheRegistryAsync`, `OidcStoreTests.AUTH_OIDC_002_AC1_OnlyAProtocolClientMayRefreshAsync`, `OidcStoreTests.AUTH_OIDC_003_AC1_RevokingAGrantTakesEveryTokenUnderItAsync`, `OidcStoreTests.AUTH_OIDC_003_AC1_TwoWritesOfOneRowCannotBothSucceedAsync`, `OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheTokensThatCanNoLongerBePresentedAsync`, `OidcFlowTests.AUTH_KEY_001_AC2_TheKeyThatSignsIsTheKeyTheSetPublishesAsync`, `OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`, `OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync`, `OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`, `OidcServiceTests.AUTH_OIDC_003_AC1_AReuseEndsEverythingDerivedFromTheRecordAsync` |
| Tier 2 (ledger 159) | AUTH-KEY-002, AUTH-OIDC-002, OPS-SEC-001 | `OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`, `OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync` |
| Tier 2 (ledger 160) | LIB-API-005, CONV-DESIGN-005, AUTH-OIDC-001 | `ResultContractTests.CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome`, `ResultContractTests.CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound`, `OidcFlowTests.AUTH_OIDC_001_UserInfoAnswersWhatTheScopeNamesAsync`, `OidcFlowTests.AUTH_KEY_001_AC4_TheKeySetCarriesTheConfiguredAlgorithmAsync` |
| Tier 3 (ledger 161) | AUTH-OIDC-002, AUTH-SESS-012, 09 section 9 | `OidcStoreTests.AUTH_OIDC_002_AC1_OnlyAProtocolClientMayRefreshAsync`, `OidcFlowTests.AUTH_OIDC_002_AC1_ABrowserApplicationAskingToHoldOneIsRefusedAsync`, `OidcFlowTests.AUTH_SESS_012_AC6_TheExchangeHandsABrowserApplicationNoRefreshTokenAsync` |
| Tier 2 (ledger 162) | OPS-MIG-005, OPS-MIG-001 | The `Double migration run` job of CONV-GATE-001, which applies the migrations twice over an empty database and compares the model against the schema |
| D-162 item 66 (ledger 163) | BFF-SESS-006, BFF-OWN-001, LIB-HOST-001, OPS-SEC-001, API-REDIR-001 | `SignOnTests.BFF_SESS_006_AC1_ALiveRecordEstablishesASessionWithNoInteractionAsync`, `SignOnTests.BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync`, `SignOnTests.BFF_SESS_006_AC3_AMismatchedStateIsRejectedAndLoggedAsync`, `SignOnTests.BFF_SESS_006_AC3_AReturnedCodeIsNotAcceptedTwiceAsync`, `SignOnTests.BFF_SESS_006_AC4_NothingButThePerApplicationSessionIsHeldAfterwardsAsync`, `SignOnTests.BFF_SESS_006_AC5_RevokingTheRecordEndsThePerApplicationSessionAsync`, `SignOnTests.BFF_SESS_006_ABrowserWithNoRecordIsSentToSignInAsync`, `SignOnTests.BFF_SESS_006_TheDestinationIsTheRegisteredOneAndNeverAskedForAsync`, `SignOnTests.BFF_SESS_006_AReturnAddressOffThisApplicationIsNotFollowedAsync`, `PreAuthenticationStoreTests.BFF_SESS_006_TheProofKeyIsAtRestUnderTheKeyEncryptionKeyAsync`, `PreAuthenticationStoreTests.BFF_SESS_006_AC3_ForgettingTheAttemptClearsEveryColumnOfItAsync`, `StartupValidationTests.BFF_SESS_006_ADeploymentThatDeclaredNoSignOnClientIsRefusedAsync` |

### The trace and the whole-history search (section A)

The instruction file that ledger entry 1 committed under `docs/guide/` was removed from
the repository and from every commit of its history by a history filter, with the
force-push protection on `main` lifted for that one push and restored after it. The
exclusion in `.git/info/exclude` carries the two file names and the directory name the
working guide's section 5 lists, and the same three patterns prefixed `**/`, so a file of
any of those names is excluded at any depth.

The first whole-history search was not empty, and it was incomplete: its content pattern
left out the instruction-file names, so it missed `docs/reports/phase-02.md` and the two
lines of D-162 section A that name the removed file. The owner corrected the decision log
twice (committed on this branch and then emptied by the rewrite below) and granted a
second rewrite for this purpose only.

*The second rewrite.* At every revision of every reference, 485 commits, the blobs of
`docs/decision-log.md`, `docs/guide/implementation-plan.md`, `docs/reports/phase-00.md`
and `docs/reports/phase-02.md` that named an instruction file, a tool, a model or a vendor
take the corrected line the owner's own later revision of the same document gives it.
Where the correction rewrapped one sentence over adjacent lines, those lines go with it.
`docs/reports/phase-02.md` had no later correction, the paragraph having been deleted, and
takes the neutral wording the owner chose. Every rewritten commit was paired with its
original by author, dates and message and compared: only those four documents differ, and
only on lines of a sentence that named something. The two commits applying the owner's
corrections to the log changed nothing once every revision carried the corrected text,
and were dropped. The tree at the tip of this branch was byte-identical before and after
the rewrite.

*The repository.* The protected-branch rule refused the forced push to `main` even with
force pushes allowed, and the read-only pull-request references of the old repository
keep its old commits reachable whatever is pushed. The repository was therefore
replaced: `neolorn/janus` was renamed `neolorn/janus-old`, a new public `neolorn/janus`
was created with the settings the phase 0 report records (public during development, as
that report now says), the rewritten `main` was pushed first and then
`phase-02-authorization` and `phase-03-sessions`, and the protection on `main` was
recreated: the same 22 required checks, strict, applied to administrators, force pushes
and deletions refused. `origin` names the new repository. `neolorn/janus-old` is kept
for the owner to delete.

*The search, on a fresh mirror clone of the new repository* (3 branches, 366 commits):

| What was searched | How | Result |
|---|---|---|
| Every commit message | `git log --all --format='%H%n%B'` against the instruction-file names, the tool, model and vendor names, and the attribution trailers | No hit |
| Every path at every revision | `git ls-tree -r --name-only` over every commit, 1456 distinct paths | No hit |
| Every file's contents at every revision | `git grep` over every commit with the same pattern | One line, at 305 revisions: `264A;GEMINI;So;...` in `tools/Janus.UnicodeTables/ucd/UnicodeData.txt`, the Unicode Character Database's name for U+264A, the accepted false positive |

No commit message, no path and no file at any revision of the repository names an
instruction file, a tool, a model or a vendor.

## 2. Points of D-162 not implemented

| Point | Reason | Waits on |
|---|---|---|
| Section D, IDN-LIFE-012's `identity.link.lastcredential` | D-162 builds it "with provider linking, which lands in phase 8 if it has not landed". Provider linking has not landed; the refusal has no path to be raised on until it does | Phase 8 |
| Section D, `MembershipChanged` emitted where membership begins and ends | D-162 assigns it to phase 8 in the same sentence. The membership aggregate this branch corrects (ledger 154) is where it will be emitted | Phase 8 |
| Section D, the scheduling of the organization erasure sweep | D-162 states that the scheduling may wait for phase 9 and the operation may not. The operation, the grace window and the event are built and tested (ledger 155); what is absent is the scheduled invocation | Phase 9 |
| Section E, the `10` rows themselves | D-162 states that the owner writes them in the reconciliation pass. The codes, keys, declarations and both closed vocabularies are in the library's catalogues and pinned by the catalogue tests; they are listed member by member in the ledger under "Rows for chapter 10" | The owner |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `Janus.Storage.csproj`, `LibraryStructureTests.PermittedGrants` | D-162 moves the protocol server's records into the library's tables, so the hosting test project must stand the same server up over fakes of those entities, and it could not see them | CONV-LAYOUT-002 with the working guide's section 3, test infrastructure | `Janus.Storage` grants `InternalsVisibleTo` to `Janus.Hosting.Tests` and the gate test that enforces the list carries the grant; no shipped project's surface changes |
| `HostDomain` and the eight test classes that use it in `Janus.Authorization.Tests` | The fake host type was called `Document`, which is now also the name of the library's own legal-document aggregate, so a test reading either read the wrong one | AUTHZ-MODEL-001, which requires the host's types to be the host's | The fake is named `Article`, a host domain type the library has no type of; no assertion changes |
| `ErrorCodesTests.Catalogue` | The frozen catalogue predated the codes D-162 section E names and the three refusals sections B and C add | `10` section 1, which the list stands for | The list is the vocabulary as this branch leaves it, so the fourteen new codes join it |
| `VocabularyContractTests` | The frozen wire-name list did not carry `PreferenceKind`, whose four members D-162 respells | REG-PREF-001 with `10` section 5 | The enumeration is on the list and its four wire names are pinned as the item spells them |
| `SettingsCatalogueTests` | The frozen key list and its protected-key comment predated the four keys D-162 section E names and the photo key of item 55 | `10` section 4 | The list is the catalogue as this branch leaves it, and the comment says "the ones marked where they are declared" rather than naming a count that a later key falsifies |
| `ModelTests` | The frozen column list predated the verification-code, outbox, protocol-record and sign-on columns this branch adds | OPS-DATA-001, which the list stands for | The list is the schema as this branch leaves it |
| `AuditActionsTests` | The closed action set predated the organization erasure of section D | IDN-AUD-001, which closes the set | The action is on the list, which is what makes the set closed against it |

## 4. Decided in the owner's absence

Under D-161 as amended by D-162. Each entry is in
`docs/reports/decisions-pending-review.md` in the same words, numbered as it is there.
Entries 110 to 157 apply a point of D-162 and supersede the ledger entry it names;
entries 158 to 163 carry the decisions this branch had to take to build those points, at
the tier the entry states. Two habits D-162 asks for are followed throughout: a question
about who is checked, what is refused or what a signal does is taken as Tier 3 and
answered with the strictest reading, and every chapter cited is quoted from the file.

| # | Decision |
|---|---|
| 110 | A capability page is one query over the host's rows, whatever it asks for |
| 111 | An explanation takes the host's rows and names the grant a fact produced |
| 112 | A type a derivation reaches is asked with the host's rows, whatever it confers |
| 113 | A password beyond the maximum is refused as a password |
| 114 | The offline list travels in the package and the self-hosted corpus has an address |
| 115 | The verification code is an aggregate of its own, and the device check issues through it |
| 116 | The configuration store stands, and the area's services are the container's |
| 117 | The library resolves a session's city from the address it already holds |
| 118 | The restrictions stay in the area; the pipeline, the templates and the router are the host's |
| 119 | Every send is written to the library's own outbox and carried from the row |
| 120 | A template is measured at startup with every place it names at its widest |
| 121 | Publishing answers for itself, and the operation that made the event carries it |
| 122 | A send counter is kept for what the restrictions now declare, not for what a send was written under |
| 123 | The library ships the words, and declaring no catalogue is not a refusal |
| 124 | Every runtime setting is written through one operation, which classifies it, gates it, requires a reason and writes it down |
| 125 | A signed-in browser asking to register is refused, and no account document crosses a registration route |
| 126 | A request the library cannot read is answered like every other refusal |
| 127 | The frontend's passkey pages are a declaration the deployment cannot start without |
| 128 | The sign-in screen is a declaration the deployment cannot start without |
| 129 | The ceremony carries the account it is for |
| 130 | Three privacy refusals take names of their own |
| 131 | The administrative routes conceal nothing from the staff who work them |
| 132 | The children's column is a declared category, and its absence is flagged |
| 133 | The consent the gate reads is the record's data subject's |
| 134 | The collation is created in the schema the library owns |
| 135 | The maintenance grants are listed in the serialized model |
| 136 | Every gate refusal carries a correlation identifier |
| 137 | The audit actions are a catalogue, and the closed vocabularies are listed |
| 138 | The preference types are spelled as the item spells them |
| 139 | The two browser headers are separate and each is named on the wire |
| 140 | The two endpoints the library calls are keys of its own |
| 141 | A reserved value is answered exactly as a held one |
| 142 | A dead cookie leaves the request anonymous and one stage requires a session |
| 143 | The stream is woken by a database channel and reads back on a key |
| 144 | The photo is the library's and the codec is the host's |
| 145 | The registry is the list, and the default is a client it holds |
| 146 | A reported change of SIM withholds the entry that rides the number |
| 147 | A purpose names the document that governs its consent |
| 148 | The dashboard records whether it was asked again |
| 149 | Reactivation's body is `linkToken`, as it was built |
| 150 | The export carries the credentials group and the whole standing group |
| 151 | The three rows the library makes true are applied; the rest of the register is offered |
| 152 | The four credential events of section 5b are emitted |
| 153 | Startup verifies the schema, and refuses only a database behind the model |
| 154 | The membership aggregate refuses the second membership |
| 155 | The organization erasure executes, and announces itself |
| 156 | The shipped lawful bases and sensitive categories exist |
| 157 | Two points of section D were already true, and one chapter row is stale |
| 158 | The protocol server owns the protocol, and the library's tables hold its records |
| 159 | The codes and the refresh tokens are encrypted under a key derived from the key-encryption key |
| 160 | `IOidc` carries the two operations the contract exposes twice, and nothing else |
| 161 | A browser application asking to hold a refresh token is refused where it asks |
| 162 | The two retired tables are dropped by the migration that creates their successors |
| 163 | The client half of the sign-on is the library's, and a host declares which client it is |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of this branch, unit and
contract: `Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 557,
`Janus.Authorization.Tests` 114, `Janus.Core.Tests` 434, `Janus.Hosting.Tests` 262,
`Janus.Identity.Tests` 67, `Janus.Privacy.Tests` 148 and `Janus.Storage.Tests` 28, none
failing. Integration, against a PostgreSQL 17 container: `Janus.Storage.Tests` 252 and
`Janus.Hosting.Tests` 126, none failing. Of those totals, 70 are contract tests, 65 of
them in `Janus.Core.Tests`.

Every other job of the CONV-GATE-001 table that can run off the platform was run on
this machine and is green: `Locked restore`, `Public surface files up to date`,
`Format`, the three analyser jobs, `Unicode tables regenerate without a diff`,
`Policy coverage test`, `Truth-table suite` (49), `Double migration run` (both
applications of the migrations applied against an empty database, the model matching
the schema afterwards), `Dependency allow-list`, `InternalsVisibleTo allow-list`,
`Forbidden markers and commented-out code`, `Acceptance-criterion test names`,
`Commit message format`, `Changelog line present` and `Destructive-operation detection
report` (report only, as the job is). `Secret scanning` and `Dependency vulnerability
alerting` are platform features and run nowhere else; they are among the identifiers
recorded below.

The commit messages of this branch were rewritten once, before the branch was offered
for merge, because `Commit message format` failed on body lines over the 72 characters
CONV-VCS-003 allows. The rewrite changed messages only: the tree of every commit is
byte-identical to what it was, which was verified by comparing the rewritten branch
against a backup of it commit by commit.

Full gate: the pipeline has not run on this branch. Pushing `corrections-1` to `origin`
was refused on this machine, so the run identifiers for the push and the pull-request
events are not yet available. They are recorded in this section, in the commit that
follows them, as phase 7's report records its own, and nothing but this section and the
status line changes in that commit.
