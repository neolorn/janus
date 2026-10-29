# Round P: people without an account

## 1. Items implemented

Criteria proven by tests that existed before the round, and still pass, are marked "at
base". The project is given where two test classes share a name.

**`21` people without an account**

- REC-REC-001:
  - AC1: `PersonRecordStoreTests.REC_REC_001_AC1_ARecordsSubjectIsNeverAnAccountsAndNeverReissuedAsync`.
  - AC2: `PersonalFieldsTests.REC_REC_001_AC2_AFieldUnderARecordsSubjectDecryptsUnderItAloneAsync`.
  - AC3:
    - `GrantEndpointTests.REC_REC_001_AC3_AGrantToASubjectNoAccountBearsIsRefusedAsync`;
    - `GroupEndpointTests.REC_REC_001_AC3_AMemberNoAccountBearsIsRefusedAsync`;
    - `TruthTableTests.REC_REC_001_AC3_AGrantAndAGroupMemberNamingAPersonRecordAreRefusedAsync`;
    - `PersonRecordStoreTests.REC_REC_001_AC3_NoMembershipNamesARecordAsync`.
  - AC4:
    - `PersonRecordStoreTests.REC_REC_001_AC4_ARecordHoldsAtMostOneEmailAndOnePhoneAsync`;
    - `PersonRecordStoreTests.REC_REC_001_AC4_ADumpHoldsNoFormOfAContactOrAReferenceAsync`.
  - Also: `PersonRecordStoreTests.REC_REC_001_ARecordReadsBackAsItWasWrittenAsync`.
- REC-REC-002:
  - AC1:
    - `PersonRecordTests.REC_REC_002_AC1_ARecordIsCreatedActive`;
    - `PersonRecordStoreTests.REC_REC_002_AC1_EveryRecordHasAStateAsync`.
  - AC2:
    - `PersonRecordTests.REC_REC_002_AC2_ARestrictionIsHeldThroughADeletionWindow`;
    - `PersonRecordTests.REC_REC_002_AC2_ACancelledDeletionReturnsTheRecordAsItWas`;
    - `PersonRecordTests.REC_REC_002_AC2_AReversedTakedownReturnsWhatTheRecordHeld`.
  - AC3: `PersonRecordTests.REC_REC_002_AC3_ATransitionTheStateDoesNotAdmitIsRefused`.
  - Also:
    - `PersonRecordTests.REC_REC_002_ARestrictedRecordsOwnFieldsAreNotChanged`;
    - `PersonRecordStoreTests.REC_REC_002_ATransitionIsCarriedOntoTheRowAsync`;
    - `VocabularyContractTests.REC_REC_002_ThePersonRecordStatesAndOriginsAreTheContract`.
- REC-REC-003:
  - AC1: `SubjectEraserTests.REC_REC_003_AC1_AnErasedRecordHoldsNothingThatIdentifiesThePersonAsync`.
  - AC2: `SubjectEraserTests.REC_REC_003_AC2_ARecordsErasureThatRollsBackErasesNothingAsync`.
  - Also:
    - `DeletionSweepTests.REC_REC_003_ARecordsDeletionIsErasedWhenItsOwnWindowElapsesAsync`;
    - `PersonRecordStoreTests.REC_REC_003_ADeletedRecordAnswersNoPersonalFieldAsync`;
    - `PersonRecordTests.REC_REC_003_ARecordIsErasedFromAnyStateButDeleted`.
- REC-COL-001:
  - AC1:
    - `CollectionSessionTests.REC_COL_001_AC1_NoContactIsStagedBeforeTheAgeTheSessionAsks`;
    - `CollectionSessionTests.REC_COL_001_AC1_AnUnderAgeDateLocksTheSession`;
    - `CollectionEndpointsTests.REC_COL_001_AC1_NoContactIsTakenBeforeTheAgeIsAnsweredAsync`;
    - `CollectionEndpointsTests.REC_COL_001_AC1_AnUnderAgeDateEndsTheSessionAndTakesNothingFurtherAsync`.
  - AC2: `CollectionEndpointsTests.REC_COL_001_AC2_ACodeVerifiesOnlyThroughTheBrowserThatBeganAsync`.
  - AC3: `CollectionEndpointsTests.REC_COL_001_AC3_StagingAHeldValueAnswersAsAFreshOneAsync`.
  - AC4:
    - `CollectionEndpointsTests.REC_COL_001_AC4_ABrowserSignedInIsRefusedAndStagesNothingAsync`;
    - `CollectionEndpointsTests.REC_COL_001_AC4_EveryStepRefusesABrowserSignedInAsync`.
  - AC5:
    - `CollectionEndpointsTests.REC_COL_001_AC5_AnAbandonedSessionLeavesNothingBehindAsync`;
    - `CollectionEndpointsTests.REC_COL_001_AC5_AnExpiredSessionAnswersAsEndedAndIsSweptAsync`;
    - `CollectionSessionStoreTests.REC_COL_001_AC5_AnExpiredSessionIsSweptAndReleasesItsBrowserAsync`.
  - Also:
    - `CollectionSessionTests.REC_COL_001_AContactStagedAgainReplacesTheOneOfItsKind`;
    - `CollectionSessionTests.REC_COL_001_ASessionLivesItsLifetimeFromItsCreation`;
    - `CollectionSessionTests.REC_COL_001_ASessionTakesAnEmailOrAPhoneAndVerifiesOnlyWhatItStaged`;
    - `CollectionSessionTests.REC_COL_001_AnAgeTheSessionDoesNotAskOrHoldsIsNotTaken`;
    - `CollectionSessionStoreTests.REC_COL_001_ASessionReadsBackAsItWasStagedAsync`;
    - `CollectionSessionStoreTests.REC_COL_001_ADumpHoldsNoFormOfAStagedContactAsync`;
    - `CollectionEndpointsTests.REC_COL_001_ACodeIsJudgedAgainstTheContactStagedLastAsync`;
    - `CollectionEndpointsTests.REC_COL_001_AMalformedContactIsRefusedAsync`;
    - `CollectionEndpointsTests.REC_COL_001_ASessionIsNotCreatedWithoutSayingWhetherItAsksTheAgeAsync`;
    - `CollectionEndpointsTests.REC_COL_001_AnAgeTheSessionDoesNotAskOrHoldsIsOutOfOrderAsync`.
- REC-COL-002:
  - AC1:
    - `RecordCollectionTests.REC_COL_002_AC1_ACollectionWritesTheRecordItsSessionVerifiedAsync`;
    - `RecordCollectionTests.REC_COL_002_AC1_ASecondCollectionWithinTheProofsAgeReusesTheRecordAsync`;
    - `RecordCollectionTests.REC_COL_002_AC1_AnotherEmailThanTheRecordHoldsWritesANewRecordAsync`;
    - `RecordDirectoryTests.REC_COL_002_AC1_ACreatedRecordHoldsWhatTheCollectionVerifiedAsync`.
  - AC2:
    - `RecordCollectionTests.REC_COL_002_AC2_ABrowserHoldingNoSessionIsRefusedAndNothingIsWrittenAsync`;
    - `RecordCollectionTests.REC_COL_002_AC2_ABrowserSignedInIsRefusedAsAnExpiredCollectionAsync`;
    - `RecordCollectionTests.REC_COL_002_AC2_ASessionSpentAbandonedOrExpiredAdmitsNoCollectionAsync`.
  - AC3:
    - `RecordCollectionTests.REC_COL_002_AC3_ASupersededNoticeVersionIsRefusedAsync`;
    - `RecordCollectionTests.REC_COL_002_AC3_ASupersededTermsVersionIsRefusedAsync`.
  - AC4:
    - `RecordCollectionTests.REC_COL_002_AC4_TheReferenceIsKeptInItsFoldedFormAsync`;
    - `RecordDirectoryTests.REC_COL_002_AC4_AReferenceIsMatchedAndHeldInNoReadableFormAsync`.
  - AC5:
    - `RecordCollectionTests.REC_COL_002_AC5_TheVerifiedValuesAreLockedBeforeAnyRecordIsJudgedAsync`;
    - `RecordDirectoryTests.REC_COL_002_AC5_TwoCollectionsOfOnePhoneAtOnceEndWithOneRecordAsync`.
  - Also, in `RecordCollectionTests` unless marked:
    - `REC_COL_002_AContactNotWellFormedIsRefusedAsync`;
    - `REC_COL_002_AContactTheScreenTookIsKeptUnverifiedAsync`;
    - `REC_COL_002_AMemberOutsideItsBoundsIsRefusedNamingItAsync`;
    - `REC_COL_002_ANameOutsideItsRuleIsRefusedAsync`;
    - `REC_COL_002_APurposeThatDoesNotRestOnConsentIsRefusedAsync`;
    - `REC_COL_002_ATermsVersionWithNoTermsPublishedIsRefusedAsync`;
    - `REC_COL_002_ARecordOutsideTheCollectionsReachIsNotReusedAsync`;
    - `REC_COL_002_AReusedRecordGainsOnlyAContactOfAKindItHeldNoneOfAsync`;
    - `REC_COL_002_AnIncompleteSessionIsRefusedAsync`;
    - `REC_COL_002_NoNoticePublishedRefusesTheCollectionAsync`;
    - `REC_COL_002_OutsideABrowsersRequestTheCollectionIsAFaultAsync`;
    - `PersonRecordStoreTests.REC_COL_002_EveryPresentationAndAcceptanceIsKeptAsync`;
    - `RecordDirectoryTests.REC_COL_002_AReusedRecordGainsWhatTheCollectionGaveItAsync`;
    - `RecordDirectoryTests.REC_COL_002_OnlyAnActiveUnattachedRecordOfTheOrganizationIsCollectableAsync`.
- REC-COL-003:
  - AC1:
    - `RecordEntryTests.REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync`;
    - `RecordDirectoryTests.REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync`.
  - AC2: `RecordEntryTests.REC_COL_003_AC2_AConsentEnteredWithTheRecordIsRecordedAdministratorAsync`.
  - AC3: `RecordEntryTests.REC_COL_003_AC3_AnEntryWithoutTheCreatePermissionInTheOrganizationIsDeniedAsync`.
  - Also:
    - `RecordEntryTests.REC_COL_003_AnEntryOutsideItsRulesIsRefusedAndKeepsNothingAsync`, an entry
      naming none of name, email and phone among its rows;
    - `RecordEntryTests.REC_COL_003_TheEntryIsAuditedWithTheStaffMemberActingAsync`;
    - `RecordEntryTests.REC_COL_003_TheReferenceIsKeptInItsFoldedFormAsync`.
- REC-AGE-001:
  - AC1: `TruthTableTests.REC_AGE_001_AC1_ASensitiveRecordUnderAPersonRecordIsRegisteredOnItsAgeAnswerAsync`.
  - AC2: `CollectionEndpointsTests.REC_AGE_001_AC2_AnUnderAgeAnswerTakesNoContactAsync`.
  - AC3:
    - `RecordEntryTests.REC_AGE_001_AC3_AnEntryHoldsTheAgeAnswerOnlyWhereThePersonGaveOneAsync`;
    - `ResourceRegistrationTests.REC_AGE_001_AC3_ASensitiveRecordRegistersUnderAnEntryOnlyWithThePersonsAnswerAsync`.
  - Also:
    - `PersonRecordTests.REC_AGE_001_ARecordKeepsTheFirstAgeAnswerItWasGiven`;
    - `RecordEntryTests.REC_AGE_001_AnUnderAgeEntryOnAnAdultsOnlyHostWritesNothingAsync`;
    - `RecordCollectionTests.REC_AGE_001_TheRecordCarriesTheAgeAnswerFromItsCollectionAsync`.
- REC-ACC-001:
  - AC1: `RecordAccessTests.REC_ACC_001_AC1_AnAskIsAnsweredAlikeWhateverIsHeldAsync`.
  - AC2: `RecordAccessTests.REC_ACC_001_AC2_AnOlderRecordIsReachedOnlyByItsReferenceAsync`.
  - AC3: `RecordAccessTests.REC_ACC_001_AC3_RecordsOutOfReachAreNeverReachedAsync`.
  - AC4:
    - `RecordAccessTests.REC_ACC_001_AC4_ASessionIsRefusedOnceItHasEndedAsync`;
    - `RecordAccessSessionStoreTests.REC_ACC_001_AC4_AnExpiredSessionIsSweptAndReleasesItsBrowserAsync`.
  - AC5: `RecordAccessTests.REC_ACC_001_AC5_APhoneTheSignalCallsRiskIsSentNothingAsync`.
  - Also, in `RecordAccessTests` unless marked:
    - `REC_ACC_001_ABrowserSignedInIsRefusedAsync`;
    - `REC_ACC_001_AMalformedValueIsRefusedAsync`;
    - `REC_ACC_001_ARestrictedOrSelfDeletingRecordIsReachedAsync`;
    - `REC_ACC_001_AWrongCodeIsInvalidWhateverIsHeldAsync`;
    - `REC_ACC_001_EveryStepRefusesABrowserSignedInAsync`;
    - `REC_ACC_001_TheCodeIsHeldToTheSignInAttemptsAsync`;
    - `REC_ACC_001_TheSessionIsNeverExtendedAsync`;
    - `REC_ACC_001_TheStateNamesEachRecordsOrganizationAsync`;
    - `PersonRecordTests.REC_ACC_001_ACodeReachesOnlyARecordInTheStatesItNames`;
    - `RecordAccessSessionStoreTests.REC_ACC_001_ASessionReadsBackAsItWasWrittenAsync`;
    - `RecordDirectoryTests.REC_ACC_001_AValueReachesOnlyTheRecordsItMayAsync`.
- REC-ACC-002:
  - AC1: `RecordRightsTests.REC_ACC_002_AC1_AReachedRecordIsReadExportedAndItsConsentsChangedAsync`.
  - AC2:
    - `RecordRightsTests.REC_ACC_002_AC2_ADeletionByACodeIsCancelledByANewCodeAsync`;
    - `RecordRightsTests.REC_ACC_002_AC2_ACancellationAfterTheWindowIsRefusedAsync`.
  - AC3: `RecordRightsTests.REC_ACC_002_AC3_ARestrictedRecordIsExportedButNotChangedAsync`.
  - AC4: `RecordRightsTests.REC_ACC_002_AC4_ADeletionWithinAMinuteOfTheCodeStillSendsItsNoticeAsync`.
  - The sample host (LIB-TEST-004): `GuestTests.REC_ACC_002_AGuestReadsExportsAndErasesTheirRecordByACodeAsync`.
  - Also:
    - `RecordRightsTests.REC_ACC_002_ARecordOutOfReachIsAnsweredAsNoneAsync`;
    - `RecordRightsTests.REC_ACC_002_ARectificationChangesTheNameAndLanguageAsync`;
    - `RecordRightsTests.REC_ACC_002_ARequestOnARecordIsReceivedAsync`;
    - `RecordRightsTests.REC_ACC_002_TheReadGivesTheRecordWithoutItsDateOfBirthAsync`;
    - `SubjectNoticesTests.REC_ACC_002_AMessageToARecordGoesToItsNoticeContactsAsync`;
    - `ExportServiceTests.REC_ACC_002_ARecordsExportAsksNoStepUpAndIsCountedForTheRecordAsync`;
    - `ExportServiceTests.REC_ACC_002_ARecordsExportRefusesAContributorTakingALibraryNameAsync`;
    - `ExportSourceTests.REC_ACC_002_ARecordsExportHoldsItsRecordProfileIdentifiersAndPreferencesAsync`;
    - `AccountStatesTests.REC_ACC_002_ARecordsRestrictionIsFulfilledOnTheRecordAsync`;
    - `RecordDirectoryTests.REC_ACC_002_ARecordHeldForAChangeIsRectifiedAndItsDeletionCancelledAsync`;
    - `RecordDirectoryTests.REC_ACC_002_ARecordIsReadAsThePersonReadsItAsync`;
    - `RecordDirectoryTests.REC_ACC_002_TheNoticeContactsAreTheVerifiedOnesOrEveryOneAsync`.
- REC-ATT-001:
  - AC1: `RecordAttachmentTests.REC_ATT_001_AC1_AnAddedPhoneOffersARecordTheAccountAttachesAsync`.
  - AC2: `RecordAttachmentTests.REC_ATT_001_AC2_ACodeToAVerifiedPhoneFindsALaterRecordAsync`.
  - AC3: `RecordAttachmentTests.REC_ATT_001_AC3_AnOlderRecordIsAttachedOnlyByItsReferenceAsync`.
  - AC4: `PersonalFieldsTests.REC_ATT_001_AC4_AFieldOfARecordStillDecryptsOnceItIsAttachedAsync`.
  - AC5: `RecordAttachmentTests.REC_ATT_001_AC5_AnAccountNoLongerActiveAttachesNothingAsync`.
  - The sample host (LIB-TEST-004): `GuestTests.REC_ATT_001_AGuestWhoRegistersLaterAttachesTheirRecordAsync`.
  - Also:
    - `RecordAttachmentTests.REC_ATT_001_AFindNamesAnIdentifierOfTheAccountAsync`;
    - `RecordAttachmentTests.REC_ATT_001_ARecordOutOfReachIsNeverOfferedAsync`;
    - `RecordAttachmentTests.REC_ATT_001_AReferenceOutOfLengthIsMalformedAsync`;
    - `RecordAttachmentTests.REC_ATT_001_AReferenceWithNoOfferIsRefusedAsync`;
    - `RecordAttachmentTests.REC_ATT_001_ARestrictedAccountIsRefusedAnAttachmentAsync`;
    - `PersonRecordTests.REC_ATT_001_AnAttachmentKeepsTheRecordAndIsNeverReplaced`;
    - `RecordDirectoryTests.REC_ATT_001_AnActiveUnattachedRecordIsAttachedOnceAsync`;
    - `RecordOfferStoreTests.REC_ATT_001_AnOfferReadsBackAndTheLatestStandsAsync`;
    - `RecordOfferStoreTests.REC_ATT_001_AnOfferEndsWhenAttachedOrSweptAsync`.
- REC-ATT-002:
  - AC1: `AttachedRecordsTests.REC_ATT_002_AC1_RestrictingAnAccountRestrictsEachRecordAttachedToItAsync`.
  - AC2:
    - `DeletionSweepTests.REC_ATT_002_AC2_AnAccountsErasureErasesEachRecordAttachedToItAsync`;
    - `AttachedRecordsTests.REC_ATT_002_AC2_TheAccountsErasureReadsEachRecordStillAttachedAsync`.
  - AC3: `RecordAccessTests.REC_ATT_002_AC3_ACodeForAnAttachedRecordsContactDoesNotReachItAsync`.
  - AC4: `AttachedRecordsTests.REC_ATT_002_AC4_ATakedownTakesEachRecordDownAndItsReversalReturnsEachAsync`.
  - AC5: `AttachedRecordsTests.REC_ATT_002_AC5_ARestrictionDecidedWhileSuspendedRestrictsEachRecordAtOnceAsync`.
  - The host read and a record's own staff paths:
    - `RecordAdministrationTests.REC_ATT_002_TheAttachedRecordsAreThoseTheOrganizationAskedInHoldsAsync`;
    - `RecordAdministrationTests.REC_ATT_002_AnAccountOutsideTheReachIsAnsweredAsNoneAsync`;
    - `RecordTakedownTests.REC_TKD_001_AC3_AnAttachedRecordFollowsItsAccountAsync`;
    - `RecordAdministrationTests.REC_ADM_002_ACorrectionIsRefusedWithoutReasonInItsWindowOrAttachedAsync`.
  - Also:
    - `AttachedRecordsTests.REC_ATT_002_LiftingAnAccountsRestrictionLiftsEachRecordsAsync`;
    - `AttachedRecordsTests.REC_ATT_002_AnAccountsDeletionWindowAndItsCancellationReachEachRecordAsync`;
    - `AttachedRecordRightsTests.REC_ATT_002_AnAttachedRecordsRightsAreExercisedThroughTheAccountAsync`;
    - `AttachedRecordRightsTests.REC_ATT_002_AnAttachedRecordsExportIsAStepUpActionAsync`;
    - `AttachedRecordRightsTests.REC_ATT_002_ARecordNotAttachedToTheAccountIsAnsweredAsNoneAsync`;
    - `AttachedRecordRightsTests.REC_ATT_002_ADeletingRecordIsReadAndNotChangedAsync`;
    - `ExportServiceTests.REC_ATT_002_AnAccountsExportNamesItsRecordsAfterItsObjectionsAsync`;
    - `ExportServiceTests.REC_ATT_002_AnAttachedRecordsExportAsksTheAccountsStepUpAsync`;
    - `ExportSourceTests.REC_ATT_002_AnAccountsRecordsNameEachRecordAttachedToItAsync`;
    - `PrivacyRequestTests.REC_ATT_002_ARequestNamingAnAttachedRecordIsTheAccountsAsync`.
- REC-ADM-001:
  - AC1:
    - `RecordAdministrationTests.REC_ADM_001_AC1_AHolderReadsARecordItsOrganizationHoldsAsync`;
    - `RecordAdministrationTests.REC_ADM_001_AC1_ARecordAnotherOrganizationHoldsIsAnsweredAsNoneAsync`;
    - `RecordAdministrationTests.REC_ADM_001_AC1_AHolderInTheAdministrativeOrganizationReadsEitherAsync`;
    - the contact and standing reads: `AccountReadTests.REC_ADM_001_AC1_ARecordBeyondTheReachIsAnsweredAsNoneAsync` (Authentication).
  - AC2:
    - `RecordFindTests.REC_ADM_001_AC2_FindingByAPhoneInArabicIndicDigitsAnswersTheRecordHoldingItAsync`;
    - `RecordFindTests.REC_ADM_001_AC2_FindingByAReferenceAnswersTheRecordsHoldingItAsync`;
    - `RecordFindTests.REC_ADM_001_AC2_FindingByAnEmailReadsItInItsCanonicalFormAsync`;
    - `RecordDirectoryTests.REC_ADM_001_AC2_AFindingAnswersTheRecordsOfTheOrganizationHoldingTheValueAsync`.
  - AC3:
    - `AccountReadTests.REC_ADM_001_AC3_TheContactReadOfARecordAnswersItsNameContactsLanguageAndStateAsync` (Authentication);
    - `AccountReadTests.REC_ADM_001_AC3_TheReadsOfARecordNameItsKindWithinTheReachAsync` (Hosting);
    - `AccountAdministrationEndpointTests.REC_ADM_001_AC3_TheContactReadOfARecordNamesItsKindAsync`.
  - Also:
    - `RecordFindTests.REC_ADM_001_ABodyOfNeitherShapeIsMalformedAsync`;
    - `RecordFindTests.REC_ADM_001_AFindingInTheAdministrativeOrganizationAnswersEveryOrganizationsRecordsAsync`;
    - `RecordFindTests.REC_ADM_001_AFindingInAnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`;
    - `RecordFindTests.REC_ADM_001_AFindingWithoutTheReadPermissionInTheOrganizationIsDeniedAsync`;
    - `RecordFindTests.REC_ADM_001_AValueNoRecordHoldsAnswersAnEmptyListAsync`;
    - `StandingTests.REC_ADM_001_AStandingOfARecordNamesItsKindAsync`;
    - `RecordDirectoryTests.REC_ADM_001_TheOrganizationHoldingARecordIsReadAloneAsync`.
- REC-ADM-002:
  - AC1:
    - `PrivacyRequestTests.REC_ADM_002_AC1_AnOutOfBandErasureOfARecordBeginsItsWindowAsync`;
    - `RecordAdministrationTests.REC_ADM_002_AC1_CancellingAnOutOfBandDeletionReturnsTheRecordAsItWasAsync`.
  - AC2: `RecordAdministrationTests.REC_ADM_002_AC2_AContactStaffCorrectIsUnverifiedAndReachesNothingAsync`.
  - AC3: `RecordAdministrationTests.REC_ADM_002_AC3_AConsentWithdrawnByStaffStampsItsRecordAndIsRecordedAsync`.
  - Also, in `RecordAdministrationTests` unless marked:
    - `REC_ADM_002_ARestrictedRecordAdmitsStaffsCorrectionAsync`;
    - `REC_ADM_002_ACorrectionIsRefusedWithoutReasonInItsWindowOrAttachedAsync`;
    - `REC_ADM_002_ARecordsRestrictionIsLiftedBehindTheStepUpAsync`;
    - `REC_ADM_002_ACancellationIsRefusedForATakedownAClosedWindowOrNoneAsync`;
    - `REC_ADM_002_StaffAssembleARecordsExportForThePersonAsync`;
    - `REC_ADM_002_StaffRecordAndWithdrawAnObjectionForThePersonAsync`;
    - `REC_ADM_002_AnEntryIsRefusedWithoutReasonInItsWindowOrAttachedAsync`;
    - `PrivacyRequestTests.REC_ADM_002_OnlyAWindowARecordBeginsRaisesItsEventAsync`;
    - `AccountStatesTests.REC_ADM_002_AnOutOfBandErasureBeginsARecordsWindowAsync`;
    - `RecordDirectoryTests.REC_ADM_002_ALiftedRecordIsActiveAndTheSubscribersAreToldAsync`.
- REC-ADM-003:
  - AC1:
    - `RecordErasureTests.REC_ADM_003_AC1_AJobDeclaringRecordEraseErasesAnActiveRecordAsync`;
    - `RecordAdministrationTests.REC_ADM_003_AC1_AJobDeclaringRecordEraseErasesAnActiveRecordAsync`;
    - `RecordAdministrationTests.REC_ADM_003_AC1_AStaffMembersErasureIsRecordedAsAJobsIsAsync`;
    - `SubjectEraserTests.REC_ADM_003_AC1_ARecordErasedAtOnceCarriesTheRetentionReasonAsync`.
  - AC2: `RecordAdministrationTests.REC_ADM_003_AC2_AnErasureOfADeletingRecordIsRefusedAndChangesNothingAsync`.
  - Also, `ErasureReason.Retention` and its spelling:
    - `VocabularyContractTests.LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract`;
    - `ErasureLedgerLineTests.DR_016_ALineIsReadAsTheErasureItWasWrittenFor`.
- REC-TKD-001:
  - AC1:
    - `RecordTakedownTests.REC_TKD_001_AC1_ATakedownEndsTheRecordsAccessAndNoCodeReachesItAsync`;
    - `TakedownServiceTests.REC_TKD_001_AC1_ARecordIsTakenDownWithNoSuspensionAsync`;
    - `RecordAccessSessionStoreTests.REC_TKD_001_AC1_EverySessionReachingTheRecordEndsAsync`.
  - AC2:
    - `RecordTakedownTests.REC_TKD_001_AC2_AReversedRecordReturnsToWhatItHeldAsync`;
    - `TakedownServiceTests.REC_TKD_001_AC2_AReversedRecordReturnsToWhatItHeldAsync`;
    - `TakedownServiceTests.REC_TKD_001_AC2_AReversalAfterTheWindowIsRefusedAsync`;
    - `DeletionSweepTests.REC_TKD_001_AC2_ARecordNotReversedIsErasedWhenTheWindowClosesAsync`.
  - AC3: `RecordTakedownTests.REC_TKD_001_AC3_AnAttachedRecordFollowsItsAccountAsync`.
  - Also:
    - `RecordTakedownTests.REC_TKD_001_ATakedownIsReadAndASecondErasedOrUnprovedOneIsRefusedAsync`;
    - `RecordTakedownTests.REC_TKD_001_ACallerAdmittedNowhereIsAnsweredAsForNoRecordAsync`;
    - `TakedownServiceTests.REC_TKD_001_ARecordTakenDownErasedOrAttachedIsNotTakenDownAsync`;
    - `TakedownServiceTests.REC_TKD_001_ARecordsTakedownIsReadAsAnAccountsIsAsync`;
    - `AccountStatesTests.REC_TKD_001_ARecordIsTakenDownOnceAndReversedToWhatItHeldAsync`.

**`01` identity, `03` authorization, `04` privacy, as D-221 amends them**

- IDN-ACCT-002, a record's subject never an account's: `REC_REC_001_AC1` above.
- IDN-LIFE-002a and PRIV-MINOR-001, the age at a collection and at a sensitive record
  registered under a record: the REC-AGE-001 and REC-COL-001 AC1 tests above.
- IDN-LIFE-003:
  - a person record: the attached record's takedown and reversal, `REC_ATT_002_AC4`, and
    the record's own, the REC-TKD-001 tests above;
  - an out-of-band window of an account: `PrivacyRequestTests.IDN_LIFE_003_AnOutOfBandWindowOfAnAccountRaisesItsDeletionRequestedAsync`.
- IDN-READ-003, a record's subject at the contact and standing reads: the REC-ADM-001 AC3
  tests and `StandingTests.REC_ADM_001_AStandingOfARecordNamesItsKindAsync` above.
- IDN-READ-004:
  - the reach over records: `REC_ADM_001_AC1` and `REC_ADM_001_AC3` above;
  - a job's `record:read` and `record:erase`: `AuthorizationModelTests.IDN_READ_004_AC3_AJobDeclaresTheLibraryOwnedPermissionsAJobMayExercise`.
- AUTHZ-INHERIT-002, a person record as the subject of a sensitive record:
  `REC_AGE_001_AC1` and `ResourceRegistrationTests.REC_AGE_001_AC3_ASensitiveRecordRegistersUnderAnEntryOnlyWithThePersonsAnswerAsync` above.
- PRIV-CONS-001, consent at a record's collection: `REC_COL_002_AC1` and
  `REC_COL_002_APurposeThatDoesNotRestOnConsentIsRefusedAsync` above; a consent or
  objection staff enter, with the mechanism `administrator` and the reason, `REC_ADM_002_AC3`
  and `REC_ADM_002_StaffRecordAndWithdrawAnObjectionForThePersonAsync` above; each entry
  naming the mechanism and kind in their spellings,
  `ConsentTests.PRIV_CONS_001_AnEntryNamesTheMechanismAndKindInTheirSpellingsAsync`.
- PRIV-CONS-011, "Your data" for a record: `REC_ACC_002_AC1` above, and through the
  account, `REC_ATT_002_AnAttachedRecordsRightsAreExercisedThroughTheAccountAsync`.
- PRIV-RIGHT-001, a record's rights in its access session, through its account and out
  of band: `REC_ACC_002_ARequestOnARecordIsReceivedAsync`, `REC_ATT_002_ARequestNamingAnAttachedRecordIsTheAccountsAsync` and `REC_ADM_002_AC1` above.
- PRIV-RIGHT-003, a record's export and the account's `records` section: the REC-ACC-002
  and REC-ATT-002 export tests above.
- AUTHZ-GATE-006 AC2, as D-222 amends it, the record rights the place a record's
  restriction is refused: `AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace`.
- AUTH-ABUSE-004, `record-deletion-notice` a security notice: `REC_ACC_002_AC4` above.
- PRIV-RIGHT-005c, a record's fingerprints: `REC_COL_002_AC4`, `REC_COL_003_TheReferenceIsKeptInItsFoldedFormAsync` and `REC_REC_003_AC1` above.

**`07` library contract, `09`, `10`, `14`, `18` and `20`**

- LIB-API-001: `VocabularyContractTests.REC_REC_002_ThePersonRecordStatesAndOriginsAreTheContract`
  and, for a record's step-up actions, `VocabularyContractTests.LIB_API_001_AC2_TheStepUpActionNamesAreTheContract`;
  the endpoint, error status and schema baselines (`endpoints.txt`, `error-statuses.txt`,
  `schema.txt`), brought up to date with each route, code and table; the public surface
  analysers over `PublicAPI.Unshipped.txt`.
- LIB-API-005: `IRecordCollection.CollectAsync` and `IPersonRecords`, the account's
  rights on a record attached to it among its operations (`07`), proven by the
  REC-COL-002, REC-ATT-002, REC-ADM-001, REC-ADM-002 and REC-TKD-001 tests above, and by
  `IdentityEndpointsTests.LIB_API_005_AC1_NoEndpointCarriesLogicItsServiceDoesNotAsync`
  and `IdentityEndpointsTests.LIB_API_005_AC3_EveryEndpointResolvesExactlyOneServiceOfTheContractAsync`.
- CONV-DESIGN-002 AC3, each record operation meeting its gate before it reads:
  `PublicSurfaceTests.CONV_DESIGN_002_AC3_EveryOperationMeetsTheGateBeforeItReadsOrWrites`.
- LIB-TEST-003 AC5, `TestPeople.Visitor()`: `PeopleTests.LIB_TEST_003_AC5_AStateChangingRequestThroughATestVisitorPassesTheCsrfStagesAsync`.
- LIB-TEST-004, the round's row: the two `GuestTests` above, a guest's walk-in at the
  sample's counter collecting a record with the age answered and the consent recorded,
  with a fitting registered under it.
- LIB-HOST-001, jobs: `IDN_READ_004_AC3` above.
- `09` sections 7a and 8a: the tests above, each route declared and in `endpoints.txt`.
- `09` section 7, `PUT /account/identifiers/{id}/replace` with 422
  `identity.identifier.invalid` and 409 `identity.identifier.locked`:
  `AccountApplicationTests.REG_IDENT_007_AReplaceOfNoHeldOrALockedIdentifierIsRefusedAsync`.
- `10`: each code, permission, key, vocabulary member, event and message kind the round
  uses, in the catalogues and the contract tests that compare them with `10` (at base).
- `14` sections 1 and 3: an attached record taken down with its account,
  `REC_ATT_002_AC4`, and a record's own takedown, the REC-TKD-001 tests above; the
  procedure's steps for the operator are a document, verified by reading.
- `18` FE-REC-001 to FE-REC-003, their server side: the criteria are of the host's and the
  account application's screens, which the library does not ship. The server's answer
  each screen rests on is proven by:
  - FE-REC-001 AC1 by `REC_COL_001_AC1`; AC3 by `REC_COL_002_AC3`; AC2 is the screen's
    alone.
  - FE-REC-002 AC1 by `REC_ACC_001_AC1`; AC2 by `REC_ACC_001_AC2`, the reference answered
    alike whether or not one is added; AC3 by the REC-ACC-002 tests.
  - FE-REC-003 AC1 by `REC_ATT_001_AC1`, the offer read as a count and nothing attached
    before the press; AC2 by the REC-ATT-002 rights tests.
- REG-IDENT-004: `REC_ATT_001_AC1` above.
- REG-IDENT-007: `RecordAttachmentTests.REG_IDENT_007_AReplaceProvingAHeldEmailBringsAnOfferAsync`.
- REG-SESS-007:
  - `RecordAttachmentTests.REG_SESS_007_ARegistrationProvingAHeldPhoneBringsAnOfferAsync`;
  - AC7:
    - `RegistrationServiceTests.REG_SESS_007_AC7_AVersionNotCurrentIsRefusedAndCreatesNoAccountAsync`;
    - `RegistrationServiceTests.REG_SESS_007_AC7_ADocumentWithNoVersionPublishedIsRefusedAsync`.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `POST /admin/grants` (`09` section 8):
  - What was out of step: its 422 list lacks `api.request.invalid`.
  - Governing item: IDN-ACCT-007 and REC-REC-001, which state the refusal.
  - Rule: the endpoint answers and declares it.
- `FactorCatalogueTests.AUTH_PRIN_002_AC1` (test infrastructure):
  - What was out of step: the gate flagged `PersonRecordOrigin.cs` for its member
    `Staff`.
  - Governing item: AUTH-PRIN-002 AC1, which forbids staff and customer as a property of
    a principal and exempts the vocabulary of who raised or wrote something; a record's
    origin (`10` section 5.51) is not a property of a principal.
  - Rule: the file joins the gate's allow-list, with its member name.
- `POST /records/collection/abandon` (`09` section 7a):
  - What was out of step: the section's opening refuses a signed-in browser at every
    collection endpoint with 409, and the row says 204 always.
  - Governing item: the row, as `POST /register/abandon` answers.
  - Rule: abandon answers 204 always.
- The reference of `POST /admin/records`:
  - What was out of step: no bound is given for it.
  - Governing item: `09` section 7a gives the access reference 1 to 64 characters after
    trimming, and REC-COL-003 takes the same reference.
  - Rule: one outside the bound answers 400 `api.request.malformed` naming `reference`,
    as API-CONV-003 answers a member outside its shape.
- A record's export and the date of birth:
  - What was out of step: whether the export carries it.
  - Governing item: `10` section 5.46, the record section holding the age answer, and
    PRIV-RIGHT-003, an export carrying everything kept; only the read (`09` section 7a)
    says never the date.
  - Rule: the export carries `dateOfBirth` where the answer holds it.
- A contributor's section of a record's export:
  - What was out of step: which names it may not take.
  - Governing item: `10` section 5.46, "none taking record or records either".
  - Rule: it takes no section name of the account's export, nor `record` nor `records`.
- `EndpointDeclarationTests.CONV_DESIGN_006_AC3` (test infrastructure):
  - What was out of step: the gate asks every route under `/admin` to declare
    `authz.denied`, and the routes under `/admin/records/{subject}` answer a caller
    admitted nowhere with 404 `identity.record.notfound`.
  - Governing item: `09` section 8a as D-222 reads it (Q2), a caller admitted nowhere
    answered as a subject no record bears.
  - Rule: the gate exempts the routes whose path names a person record from that
    declaration and from nothing else.
- `PublicSurfaceTests.CONV_DESIGN_002_AC3` (test infrastructure):
  - What was out of step: the gate's list of the operations on the caller's own records
    lacks those of `IPersonRecords` an account asks on itself (the records attached to
    it, the offer, the find code and the rights on an attached record), each of which
    asks whose account is asking before any call.
  - Governing item: REC-ATT-002, a person's own context reading its own, and
    AUTHZ-GATE-006, no permission governing a person's own records.
  - Rule: the list names them, and the gate reads each as it reads the account's own
    operations.
- `privacy.consent.granted`, `privacy.consent.withdrawn`, `privacy.objection.recorded` and
  `privacy.objection.withdrawn` (f45bcdc6):
  - What was out of step: `details.mechanism` and `details.kind` were written as the
    members' numbers.
  - Governing item: `10` section 5.24 as D-223 amends it, naming `details.mechanism`
    `administrator` in its section 5.21 spelling, the kind in its section 5.10 one.
  - Rule: each entry writes the spellings.
- Suppressions:
  - CA1711 on `IRecordCollection` and `RecordCollection`, each with its justification in
    the attribute: LIB-API-005 and REC-COL-002 name both.
  - CA2000 on `TestPeople.Visitor()`, with its justification in the attribute: the
    visitor is an `HttpClient` made with `disposeHandler: true`, so the browser it
    carries is disposed with it.
  - The six migrations of the round carry the `#pragma warning disable 612, 618` their
    tool writes, as phase-01 names.

## 4. Open questions

Q1 to Q11 are answered by D-222 and Q12 and Q13 by D-223, and every item they parked is
built (section 1).

**Q1 (Tier 2).** The document `termsVersion` is checked against (REC-COL-002).

- What the code needs: the document `termsVersion` is checked against, for
  `privacy.consent.superseded` naming `termsVersion`.
- What the spec says: REC-COL-002 and `10`'s `privacy.consent.superseded` row; the notice
  is named `privacy-notice`; no chapter names the terms document; REG-SESS-007 records
  `termsVersion` unchecked; the host names its terms (`TestPeople`).
- Reading A: the library names the terms document. Smallest fix: a `10` row.
- Reading B: `RecordCollection` names the document with its version. Smallest fix: `21`
  REC-COL-002 and the `07` shape.
- Reading C: recorded as given, unchecked. Smallest fix: "or `termsVersion`" leaves `21`
  and `10`.
- Parks: the `termsVersion` check and its refusal test. Built: `termsVersion` recorded
  with its instant; `noticeVersion` checked against `privacy-notice`.
- Answered by D-222: the terms and privacy-notice checks. Built in 653f7f67.

**Q2 (Tier 3).** Admission and refusal of the `/admin/records/{subject}` rows.

- `09` section 8, opening: denials are 403 and nothing is concealed; the organization is
  read from the row, and an identifier naming no row is 403 `authz.denied`, as a missing
  permission is.
- `09` section 8a, the person records heading: asked in the record's organization with
  REC-ADM-001's reach; outside the reach and no record are 404
  `identity.record.notfound` alike, in identical bytes.
- `21` REC-ADM-001: asked "in the organization named", with IDN-READ-004's reach (the
  registered data subjects); AC1 is 404 for a record another organization holds alone.
- `01` IDN-READ-004: asked in the organization named, the administrative one where none
  is; `GET /admin/records/{subject}` names none.
- The contradictions:
  1. A caller without the permission in the record's organization or the administrative
     one: 403 or 404.
  2. A subject no record bears: 403 or 404.
  3. Which organization a row naming none is asked in (the record's, the administrative
     one, or one named), and whether the registered-data-subject reach applies.
  4. Whether a 404 refusal is recorded, and against which organization.
- Parks: every `/admin/records/{subject}` route (the read, `PUT`, consents, objections,
  export, lift, delete and its cancellation, erase, takedown with its read and reversal)
  and `GET /admin/accounts/{subject}/records`: the REC-ADM-001 read, REC-ADM-002's staff
  routes, REC-ADM-003, REC-TKD-001 and REC-ATT-002's host read.
- Answered by D-222: admission of the `/admin/records` rows, a caller admitted nowhere
  and a subject no record bears answered 404 `identity.record.notfound` alike. Built in
  5ec54f8f, 13ed50a8, 0fecd09e, 496a731f, d6fd7951 and 54ec8f33; REC-ADM-002 AC3 and
  REC-ADM-003, answered by D-223, in de579f57 and 79c4d9ba.

**Q3 (Tier 2).** `POST /admin/records`: the member named where none of name, email and
phone is given.

- What the code needs: the member detail of 422 `api.request.invalid` for an entry with
  none of the three.
- What the spec says: `09` section 8a's row names "the member for ... or none of the
  three"; `21` REC-COL-003 asks for at least one of the three; no member is named.
- Reading A: name the first, `name`.
- Reading B: name each absent one, `name`, `email`, `phone`; `09` names one.
- Reading C: no member detail for this case; `09` is reworded.
- Parks: that refusal and its test. Built: the rest of the entry.
- Answered by D-222: reading A, `name`. Built in b199210e.

**Q4 (Tier 2).** `POST /admin/records`: an under-age `dateOfBirth` where
`registration.adultaffirmation` is required.

- What the code needs: what the entry does with it.
- What the spec says: REC-AGE-001 records it "as at registration: the affirmation with
  its instant where required"; registration refuses `identity.profile.underage`; `09`
  section 8a names no under-age refusal for the entry; AC3 names only "with the person's
  answer".
- Reading A: record the answer as not affirmed, the record then admitting no sensitive
  record.
- Reading B: refuse 422 `identity.profile.underage`, nothing written; `09`'s row gains it.
- Reading C: record no answer.
- Parks: the under-age branch's test and any change the answer makes. Built: an adult
  date recorded as the affirmation; the screen's computation (reading A) stands
  meanwhile, since every reading changes only that branch.
- Answered by D-222: reading B, 422 `identity.profile.underage`. Built in b199210e.

**Q5 (Tier 3).** `record-deletion-notice` under the sending restrictions.

- What the code needs: whether `record-deletion-notice`, sent to a person record's notice
  contacts, is a security notice to an existing holder, governed only by restrictions of
  purpose `notification` (`02` AUTH-ABUSE-004), as `deletion-notice` and
  `oob-deletion-notice` are.
- What the spec says: AUTH-ABUSE-004, "Security notices to an existing holder"; `10`
  section 5.25, `record-deletion-notice` "sent to its notice contacts" and
  `oob-deletion-notice` "or to a person record's notice contacts"; `21` REC-ACC-002's
  notice contacts; no chapter says whether a record's contacts are an existing holder.
- Parks: that membership. Built: `record-deletion-notice` under purpose `notification`,
  counted by every restriction as an ordinary notification is.
- Consequence until answered: under the shipped `email.destination` (1 per 60 s fixed,
  any purpose), a deletion asked within a minute of the access code goes without its
  notice; the REC-ACC-002 AC2 test asks the deletion past that minute.
- Answered by D-222: `record-deletion-notice` is a security notice. Built in cdc24b29.

**Q6 (Tier 2).** `POST /admin/records/find`: the records it searches and an unknown
organization.

- What the code needs: (1) which records a find asked in organization X searches; (2) the
  refusal for an organization the deployment does not hold.
- What the spec says: `21` REC-ADM-001 and `09` section 8a, "the records of an
  organization holding"; the same item "with IDN-READ-004's reach", which takes in a
  record another organization holds that is the data subject of a record registered in
  X; `09` section 8, 404 `identity.organization.notfound` for accounts; `09` section 8a,
  `POST /admin/records` answering 422 `api.request.invalid` naming `organization`; the
  find's row names neither.
- (1) Reading A: the records X holds. Reading B: every record in X's reach.
- (2) Reading A: 404 `identity.organization.notfound`, as section 8. Reading B: 422
  `api.request.invalid` naming `organization`, as the section 8a entry.
- Parks: whether a record another organization holds is answered, and the
  unknown-organization refusal with its test. Built: the find over the records X holds,
  which both readings answer.
- Answered by D-222: (1) reading A, a find in the administrative organization answering
  every organization's records; (2) 404 `identity.organization.notfound`. Built in
  9d6b2a56.

**Q7 (Tier 2).** `AccountDeletionRequested` on an out-of-band erasure of an account.

- What the code needs: whether a fulfilled out-of-band erasure that begins an account's
  window raises `AccountDeletionRequested`.
- What the spec says: `10` section 5, "`AccountDeletionRequested`: the grace window
  starts"; `01` IDN-LIFE-003 names it only as not firing for a takedown; `21` REC-ADM-002
  has a record's out-of-band erasure raise `RecordDeletionRequested`; the code raises
  `AccountDeletionRequested` only for the account's own deletion.
- Reading A: it fires, as `10` reads; the privacy fulfilment publishes it, as it now
  publishes `RecordDeletionRequested`.
- Reading B: it does not; `10`'s row names the self-service window alone.
- Parks: nothing in round P; the account path stands as it is until the answer.
- Answered by D-222: `AccountDeletionRequested` and `AccountDeletionCancelled` fire for
  an out-of-band window. Built in dd8b7758.

**Q8 (Tier 2).** `PUT /account/identifiers/{id}/replace`: the codes it answers that
neither `09` nor its declaration names.

- What the code needs: the answer to a replace naming an identifier the account does not
  hold or a username, to one outside single-address mode, and to a locked identifier.
- What the spec says: `09` lists 202, 403, 429, 409 and 422 `mixedscript` and
  `domainnotallowed` only; the code answers 422 `identity.identifier.invalid` for the
  first two and `identity.identifier.locked` for the third; the declaration names
  neither, so the test deployment refuses the undeclared answer.
- Reading A: `09` and the declaration gain 422 `identity.identifier.invalid` and
  `identity.identifier.locked`.
- Reading B: an identifier the account does not hold answers 404, as elsewhere in section
  7, and the rest as reading A.
- Parks: nothing in round P; a round P test found it, and runs in single-address mode.
- Answered by D-222: reading A, `identity.identifier.locked` at 409. Built in b15f3297.

**Q9 (Tier 3).** REC-ATT-002's lifecycle: a restriction a suspended account holds, and an
attachment to an account away from active.

- What the spec says: `21` REC-ATT-002, every transition of the account into or out of
  `restricted`, `deleting` and `deleted` applies to each attached record in the same
  transaction, and "an account's suspension changes no attached record"; `04`
  PRIV-RIGHT-004, a suspended account holds the restriction and comes back restricted at
  its reactivation; `21` REC-ATT-001 refuses only a restricted account's attachment.
- The contradictions:
  1. A restriction reaching a suspended account is no transition into restricted, and
     its reactivation into restricted is a transition out of suspended: whether each
     attached record is restricted when the restriction is held, when the reactivation
     brings the account into restricted, or never.
  2. An account in its deletion window, or suspended, attaching a record: whether the
     record takes the account's state at once, or stays active until the account's next
     transition.
- Parks: (1) the record side of a suspended account's held restriction and of its
  reactivation into restricted. (2) nothing new: an attachment sets the record's account
  and instant alone, and a later transition of the account applies to each record whose
  state admits it.
- Answered by D-222: the restriction follows the decision; an attachment only while the
  account is active. Built in 3a3170b8 and 85cfa6dc.

**Q10 (Tier 2).** A guest's browser for the sample host's growth (LIB-TEST-003,
LIB-TEST-004).

- What the code needs: a browser holding no sign-in session that carries the
  pre-authentication cookie, the synchronizer token, `X-Identity-Request`, the origin and
  the fetch metadata, for a guest's collection steps (`/records/collection` and its steps,
  then the host's own endpoint calling `IRecordCollection.CollectAsync`) and the record
  access steps (`/records/access` and its steps, `/records/access/{subject}` and its
  deletion), each state-changing and CSRF-checked (`09` section 7a).
- What the spec says: LIB-TEST-003 gives `TestPeople` a customer, the first administrator
  and a staff member, each a signed-in `TestSession`, and no browser without a session;
  LIB-TEST-004 has the sample's tests prove what they prove "as the people
  `Janus.Testing` signs in", an obstacle being a gap in the public surface raised as a
  question.
- Reading A: `Janus.Testing` gains a browser holding no session, for instance
  `TestPeople.Visitor()`, an `HttpClient` carrying what a `TestSession` carries without
  the session. Smallest fix: one public member and its `PublicAPI.Unshipped.txt` line,
  and LIB-TEST-003 amended.
- Reading B: the sample's tests carry their own handler doing the same, as a host's own
  frontend does, `WorkshopHost.Visitor()` keeping cookies and setting the CSRF headers.
  Smallest fix: one handler in `samples/HostSample.Tests`, no library change.
- Parks: the sample host's growth: a guest's collection under a person record with the
  age answered, a sensitive record registered under it, its read and erasure by a code,
  and the attachment of another to an account registered later.
- Answered by D-222: reading A, `TestPeople.Visitor()`, a `TestVisitor` (LIB-TEST-003
  and its AC5). Built in 460d139c and 51b79ae1.

**Q11 (Tier 3).** A person record's restriction and AUTHZ-GATE-006.

- What the spec says:
  - `03` AUTHZ-GATE-006 AC2: a restriction is enforced through the gate, not by scattered
    checks; its values (D-207): the restriction the gate judges is the acting account's.
    A record access session acts for no account, so the gate judges no record's
    restriction.
  - `09` section 7a, `PUT /records/access/{subject}`: 403 `authz.restricted` while the
    record is `restricted`; `21` REC-ACC-002 AC3: a restricted record is exported but
    not changed.
- What the code does: `RecordRights` refuses the change itself (a9e7121d), and
  `AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace` finds
  `Error.From(ErrorCodes.Restricted)` outside `AccessGate.cs` and fails.
- Parks: the push of `round-p` and its pull request, since every unit test passes before
  a push; the refusal stands meanwhile, a restricted record changed in nothing.
- Answered by D-222: AUTHZ-GATE-006 AC2 amended; the seam test is brought in line.
  Built in 532f41d5.

**Q12 (Tier 2).** REC-ADM-002 AC3: the staff consent and objection rows.

- What the code needs: where a withdrawal by staff records the mechanism `administrator`
  and the required `reason`, for `POST /admin/records/{subject}/consents/{purpose}/withdraw`
  and `DELETE /admin/records/{subject}/objections/{purpose}`.
- What the spec says: `21` REC-ADM-002 and `09` section 8a, "with the mechanism
  `administrator` and a `reason`"; AC3, a consent withdrawn by staff "recorded with the
  mechanism `administrator`"; `04` PRIV-CONS-001, a withdrawal stamps the instant on the
  consent record, which keeps the mechanism of its grant; `10` names no withdrawal
  mechanism and no place for the reason.
- Reading A: the consent and objection records gain the mechanism of their withdrawal.
  Smallest fix: one column each, a `10` row, a migration.
- Reading B: the withdrawal's audit record carries the mechanism and the reason in its
  details. Smallest fix: two `details` keys in `10`'s audit row; the records unchanged.
- Reading C: a withdrawal by staff ends the record and writes a new one with the
  mechanism `administrator`. Smallest fix: PRIV-CONS-001 states it.
- For each reading, the place of the `reason` on a grant and on an objection is also
  unnamed.
- Parks: REC-ADM-002 AC3 and the four rows. Built: nothing of them.
- Answered by D-223: reading B; a grant or objection staff enter is a record of its own
  with the mechanism `administrator`, a withdrawal stamps the record it ends, and the
  audit entry carries `details.mechanism` `administrator` and `details.reason`. Built in
  f45bcdc6 and de579f57.

**Q13 (Tier 2).** REC-ADM-003: the place of the erasure's required `reason`.

- What the code needs: where `POST /admin/records/{subject}/erase` records the `reason`
  it requires, for AC1's "recorded with its principal and reason".
- What the spec says: `21` REC-ADM-003 and `09` section 8a, `{ reason }` required and the
  erasure's reason `retention`; `10`, `privacy.erasure.executed` carrying `details.reason`
  in its section 5.12a spelling, so `retention`; the audit entry holds a stated reason
  only for a system principal (`PrincipalReason`), and a staff member calling the route
  is none.
- Reading A: the stated reason goes to `PrincipalReason` for a job and to a new
  `details` key for a person. Smallest fix: one `10` key.
- Reading B: the route is a job's alone, a person refused. Smallest fix: `09` section 8a
  and REC-ADM-003 say so.
- Reading C: the stated reason goes to `details.reason` and the spelling `retention` to
  another key. Smallest fix: `10`'s row changed for this action.
- Parks: REC-ADM-003, `IPersonRecords.EraseAsync`, the route, `ErasureReason.Retention`
  and its migration. Built: nothing of them.
- Answered by D-223: `details.reason` stays `retention`, and the reason given is
  `details.statedReason`. Built in 79c4d9ba.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed. 61a7ea53 adds the record access session tables without their lines in
  `tests/Janus.Storage.Tests/schema.txt`, which d3b67b4d adds; the storage integration
  suite fails on 61a7ea53 alone.
- The pipeline's runs on 8cc13f56: red, in the contract, integration, truth-table and
  sample-host jobs. a2e23633 to f17b435d fix each cause, the tests the runs named run
  green here:
  - the gate order of a record's contact read and of the routes under
    `/admin/records/{subject}`, and the attached record's rights carried by
    `IPersonRecords` (LIB-API-005): a2e23633, fe946c11, f02d4a67;
  - the codes the record routes declare, `endpoints.txt` brought up to date, and the
    remediation sentence of each record code: 617aaa34, f17b435d;
  - tests seeding an account without the subject key it is now made with, a fixture
    key-encryption key no other deployment held, a collection staging its contact
    before its age, a dump search under a case-insensitive collation, the fingerprint
    scan missing the record store, an erased record's key proven through a read that
    answers none, and the key rotation's reset leaving the rows that name a key:
    63b68b5d, 2c39c09a, c6d3a7a5, 42d19fdf, 68381249, 3305244c, 659af305;
  - the sample's analyser finding (CA1861): df39b986.
- The pipeline's runs on 2b8f3f61: the push run green; the pull request's run red in the
  sample-host job, a guest's code read before its message was delivered. 82da6a8b fixes
  it.
- Checks before the push: green. Every unit test passes (5010 in eleven projects), the
  commit-message gate passes over the round's commits, and the full-history secret scan
  finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
