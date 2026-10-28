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
- REC-COL-002, the check of `termsVersion` waiting on Q1:
  - AC1:
    - `RecordCollectionTests.REC_COL_002_AC1_ACollectionWritesTheRecordItsSessionVerifiedAsync`;
    - `RecordCollectionTests.REC_COL_002_AC1_ASecondCollectionWithinTheProofsAgeReusesTheRecordAsync`;
    - `RecordCollectionTests.REC_COL_002_AC1_AnotherEmailThanTheRecordHoldsWritesANewRecordAsync`;
    - `RecordDirectoryTests.REC_COL_002_AC1_ACreatedRecordHoldsWhatTheCollectionVerifiedAsync`.
  - AC2:
    - `RecordCollectionTests.REC_COL_002_AC2_ABrowserHoldingNoSessionIsRefusedAndNothingIsWrittenAsync`;
    - `RecordCollectionTests.REC_COL_002_AC2_ABrowserSignedInIsRefusedAsAnExpiredCollectionAsync`;
    - `RecordCollectionTests.REC_COL_002_AC2_ASessionSpentAbandonedOrExpiredAdmitsNoCollectionAsync`.
  - AC3, for `noticeVersion`: `RecordCollectionTests.REC_COL_002_AC3_ASupersededNoticeVersionIsRefusedAsync`.
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
    - `REC_COL_002_ARecordOutsideTheCollectionsReachIsNotReusedAsync`;
    - `REC_COL_002_AReusedRecordGainsOnlyAContactOfAKindItHeldNoneOfAsync`;
    - `REC_COL_002_AnIncompleteSessionIsRefusedAsync`;
    - `REC_COL_002_NoNoticePublishedRefusesTheCollectionAsync`;
    - `REC_COL_002_OutsideABrowsersRequestTheCollectionIsAFaultAsync`;
    - `PersonRecordStoreTests.REC_COL_002_EveryPresentationAndAcceptanceIsKeptAsync`;
    - `RecordDirectoryTests.REC_COL_002_AReusedRecordGainsWhatTheCollectionGaveItAsync`;
    - `RecordDirectoryTests.REC_COL_002_OnlyAnActiveUnattachedRecordOfTheOrganizationIsCollectableAsync`.
- REC-COL-003, the refusal of an entry naming none of the three waiting on Q3:
  - AC1:
    - `RecordEntryTests.REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync`;
    - `RecordDirectoryTests.REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync`.
  - AC2: `RecordEntryTests.REC_COL_003_AC2_AConsentEnteredWithTheRecordIsRecordedAdministratorAsync`.
  - AC3: `RecordEntryTests.REC_COL_003_AC3_AnEntryWithoutTheCreatePermissionInTheOrganizationIsDeniedAsync`.
  - Also:
    - `RecordEntryTests.REC_COL_003_AnEntryOutsideItsRulesIsRefusedAndKeepsNothingAsync`;
    - `RecordEntryTests.REC_COL_003_TheEntryIsAuditedWithTheStaffMemberActingAsync`;
    - `RecordEntryTests.REC_COL_003_TheReferenceIsKeptInItsFoldedFormAsync`.
- REC-AGE-001, the entry's under-age branch waiting on Q4:
  - AC1: `TruthTableTests.REC_AGE_001_AC1_ASensitiveRecordUnderAPersonRecordIsRegisteredOnItsAgeAnswerAsync`.
  - AC2: `CollectionEndpointsTests.REC_AGE_001_AC2_AnUnderAgeAnswerTakesNoContactAsync`.
  - AC3:
    - `RecordEntryTests.REC_AGE_001_AC3_AnEntryHoldsTheAgeAnswerOnlyWhereThePersonGaveOneAsync`;
    - `ResourceRegistrationTests.REC_AGE_001_AC3_ASensitiveRecordRegistersUnderAnEntryOnlyWithThePersonsAnswerAsync`.
  - Also:
    - `PersonRecordTests.REC_AGE_001_ARecordKeepsTheFirstAgeAnswerItWasGiven`;
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
- REC-ACC-002, the membership of `record-deletion-notice` waiting on Q5:
  - AC1: `RecordRightsTests.REC_ACC_002_AC1_AReachedRecordIsReadExportedAndItsConsentsChangedAsync`.
  - AC2:
    - `RecordRightsTests.REC_ACC_002_AC2_ADeletionByACodeIsCancelledByANewCodeAsync`;
    - `RecordRightsTests.REC_ACC_002_AC2_ACancellationAfterTheWindowIsRefusedAsync`.
  - AC3: `RecordRightsTests.REC_ACC_002_AC3_ARestrictedRecordIsExportedButNotChangedAsync`.
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
- REC-ATT-002, the host read and the record's own staff paths waiting on Q2, and the
  record side of a suspended account's held restriction waiting on Q9:
  - AC1: `AttachedRecordsTests.REC_ATT_002_AC1_RestrictingAnAccountRestrictsEachRecordAttachedToItAsync`.
  - AC2:
    - `DeletionSweepTests.REC_ATT_002_AC2_AnAccountsErasureErasesEachRecordAttachedToItAsync`;
    - `AttachedRecordsTests.REC_ATT_002_AC2_TheAccountsErasureReadsEachRecordStillAttachedAsync`.
  - AC3: `RecordAccessTests.REC_ATT_002_AC3_ACodeForAnAttachedRecordsContactDoesNotReachItAsync`.
  - AC4: `AttachedRecordsTests.REC_ATT_002_AC4_ATakedownTakesEachRecordDownAndItsReversalReturnsEachAsync`.
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
- REC-ADM-001, the read `GET /admin/records/{subject}` waiting on Q2, and the find's
  reach beyond the organization's own records and its unknown organization waiting on Q6:
  - AC1, the contact and standing reads: `AccountReadTests.REC_ADM_001_AC1_ARecordBeyondTheReachIsAnsweredAsNoneAsync` (Authentication).
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
    - `RecordFindTests.REC_ADM_001_AFindingWithoutTheReadPermissionInTheOrganizationIsDeniedAsync`;
    - `RecordFindTests.REC_ADM_001_AValueNoRecordHoldsAnswersAnEmptyListAsync`;
    - `StandingTests.REC_ADM_001_AStandingOfARecordNamesItsKindAsync`.
- REC-ADM-002, the out-of-band request alone; its staff routes wait on Q2:
  - AC1, the request and its window: `PrivacyRequestTests.REC_ADM_002_AC1_AnOutOfBandErasureOfARecordBeginsItsWindowAsync`.
  - Also:
    - `PrivacyRequestTests.REC_ADM_002_OnlyAWindowARecordBeginsRaisesItsEventAsync`;
    - `AccountStatesTests.REC_ADM_002_AnOutOfBandErasureBeginsARecordsWindowAsync`.

**`01` identity, `03` authorization, `04` privacy, as D-221 amends them**

- IDN-ACCT-002, a record's subject never an account's: `REC_REC_001_AC1` above.
- IDN-LIFE-002a and PRIV-MINOR-001, the age at a collection and at a sensitive record
  registered under a record: the REC-AGE-001 and REC-COL-001 AC1 tests above.
- IDN-LIFE-003, a person record: the attached record's takedown and reversal,
  `REC_ATT_002_AC4` above; the takedown by a record's own path waits on Q2 (REC-TKD-001).
- IDN-READ-003, a record's subject at the contact and standing reads: the REC-ADM-001 AC3
  tests and `StandingTests.REC_ADM_001_AStandingOfARecordNamesItsKindAsync` above.
- IDN-READ-004:
  - the reach over records: `REC_ADM_001_AC1` and `REC_ADM_001_AC3` above;
  - a job's `record:read` and `record:erase`: `AuthorizationModelTests.IDN_READ_004_AC3_AJobDeclaresTheLibraryOwnedPermissionsAJobMayExercise`.
- AUTHZ-INHERIT-002, a person record as the subject of a sensitive record:
  `REC_AGE_001_AC1` and `ResourceRegistrationTests.REC_AGE_001_AC3_ASensitiveRecordRegistersUnderAnEntryOnlyWithThePersonsAnswerAsync` above.
- PRIV-CONS-001, consent at a record's collection: `REC_COL_002_AC1` and
  `REC_COL_002_APurposeThatDoesNotRestOnConsentIsRefusedAsync` above.
- PRIV-CONS-011, "Your data" for a record: `REC_ACC_002_AC1` above, and through the
  account, `REC_ATT_002_AnAttachedRecordsRightsAreExercisedThroughTheAccountAsync`.
- PRIV-RIGHT-001, a record's rights in its access session, through its account and out
  of band: `REC_ACC_002_ARequestOnARecordIsReceivedAsync`, `REC_ATT_002_ARequestNamingAnAttachedRecordIsTheAccountsAsync` and `REC_ADM_002_AC1` above.
- PRIV-RIGHT-003, a record's export and the account's `records` section: the REC-ACC-002
  and REC-ATT-002 export tests above.
- PRIV-RIGHT-005c, a record's fingerprints: `REC_COL_002_AC4`, `REC_COL_003_TheReferenceIsKeptInItsFoldedFormAsync` and `REC_REC_003_AC1` above.

**`07` library contract, `09`, `10`, `14`, `18` and `20`**

- LIB-API-001: `VocabularyContractTests.REC_REC_002_ThePersonRecordStatesAndOriginsAreTheContract`;
  the endpoint, error status and schema baselines (`endpoints.txt`, `error-statuses.txt`,
  `schema.txt`), brought up to date with each route, code and table; the public surface
  analysers over `PublicAPI.Unshipped.txt`.
- LIB-API-005: `IRecordCollection.CollectAsync` and `IPersonRecords`, proven by the
  REC-COL-002 and REC-ADM-001 tests above.
- LIB-HOST-001, jobs: `IDN_READ_004_AC3` above.
- `09` section 7a, and section 8a for the entry, the find, the contact and standing reads
  and the out-of-band request: the tests above, each route declared and in
  `endpoints.txt`.
- `10`: each code, permission, key, vocabulary member, event and message kind the round
  uses, in the catalogues and the contract tests that compare them with `10` (at base).
- `14` sections 1 and 3: an attached record taken down with its account, `REC_ATT_002_AC4`
  above; a record's own takedown waits on Q2.
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
- REG-SESS-007: `RecordAttachmentTests.REG_SESS_007_ARegistrationProvingAHeldPhoneBringsAnOfferAsync`.

## 2. Items not implemented

- REC-ADM-001, the read `GET /admin/records/{subject}`: waits on Q2.
- REC-ADM-002, every staff route under `/admin/records/{subject}`, AC1's cancellation by
  staff, AC2 and AC3: waits on Q2.
- REC-ADM-003: waits on Q2.
- REC-TKD-001, with IDN-LIFE-003's person record and `14` sections 1 and 3 for a record
  taken down by its own path: waits on Q2.
- REC-ATT-002:
  - `IPersonRecords.AttachedAsync` and `GET /admin/accounts/{subject}/records`, and the
    refusal `identity.record.followsaccount` on a record's own staff paths: wait on Q2;
  - the record side of a restriction a suspended account holds, and of its reactivation
    into restricted: waits on Q9.
- REC-COL-002, the check of `termsVersion`: waits on Q1.
- REC-COL-003, the refusal of an entry naming none of name, email and phone: waits on Q3.
- REC-AGE-001, the entry's under-age branch: waits on Q4.
- REC-ACC-002, whether `record-deletion-notice` is a security notice under the sending
  restrictions: waits on Q5.
- REC-ADM-001, the find's records beyond those the organization holds and its unknown
  organization: wait on Q6.
- The sample host's growth (LIB-TEST-004, the round's row): waits on Q10.
- The push of the round and its pull request: wait on Q11; REC-ACC-002's refusal of a
  restricted record's change, built, fails `AccessSeamTests.AUTHZ_GATE_006_AC2`.

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
- Suppressions:
  - CA1711 on `IRecordCollection` and `RecordCollection`, each with its justification in
    the attribute: LIB-API-005 and REC-COL-002 name both.
  - The six migrations of the round carry the `#pragma warning disable 612, 618` their
    tool writes, as phase-01 names.

## 4. Open questions

None is answered yet.

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


## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed. 61a7ea53 adds the record access session tables without their lines in
  `tests/Janus.Storage.Tests/schema.txt`, which d3b67b4d adds; the storage integration
  suite fails on 61a7ea53 alone.
- Checks before the push: the commit-message gate is green; every unit test but
  `AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace` passes
  (Q11), so nothing is pushed and no pipeline has run on the round.
