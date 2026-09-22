# Phase 7: Privacy

Status: complete, full gate green, no open question. What a deployment processes and
on what basis, with the closed category list and the capture path a consent runs
through; the consent and objection records, written consent among them, with the
supersession a material revision of the notice causes; the legal documents with one
governing language, their translations and their published versions; the gate that
refuses an action whose purpose rests on a consent nobody gave; the data subject
request queue on the working-day clock with the deployment's holidays; the export an
account takes of what is held about it, in both formats, behind step-up and a rolling
daily limit; deactivation, deletion and the reactivation link, the sweep that erases
what has passed its window, and the subject events every registered handler confirms;
the audit trail that still answers who was affected after an erasure; the records of
processing generated from the declaration; and the startup checks that stop a
deployment whose declared categories have no retention period, whose service is open
to minors with no written-consent basis, or whose encrypted field names no subject
column, are built. The decisions taken in the owner's absence are in section 4 and, in
the same words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| PRIV-PRIN-001 | AC1, AC2 | `DeclaredProcessingTests.PRIV_PRIN_001_AC1_EachPurposeNamesTheCategoriesItRequires`, `DeclaredProcessingTests.PRIV_PRIN_001_AC2_AFieldHeldByNoDeclaredPurposeFailsValidation` |
| PRIV-PRIN-002 | AC1, AC2 | `ProcessingRecordsTests.PRIV_PRIN_002_AC1_AddingAPurposeChangesTheRegisterWithNoSeparateEditAsync`, `ProcessingRecordsTests.PRIV_PRIN_002_AC2_NoInventoryOutlivesTheDeclarationAsync` |
| PRIV-BASIS-001 | AC1, AC2, AC3, AC4, AC5 | `DeclaredProcessingTests.PRIV_BASIS_001_AC1_APurposeOnAnUndeclaredBasisFailsStartup`, `DeclaredProcessingTests.PRIV_BASIS_001_AC3_APurposeWithoutABasisIsRefusedWhereItIsDeclared`, `DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis`, `DeclaredProcessingTests.PRIV_BASIS_001_AC5_AnotherListWithOtherFlagsNeedsNoLibraryChange`, `ProcessingRecordsTests.PRIV_BASIS_001_AC2_TheBasisColumnCarriesTheDeclaredLabelAsync` |
| PRIV-BASIS-002 | AC1, AC2 | `DeclaredProcessingTests.PRIV_BASIS_002_AC1_APurposeOnAnAssessedBasisWithoutOneFailsStartup`, `ProcessingRecordsTests.PRIV_BASIS_002_AC2_TheAssessmentReferenceIsOnTheRowAsync` |
| PRIV-BASIS-003 | AC1 | `DeclaredProcessingTests.PRIV_BASIS_003_AC1_TheOrdinaryPathOverSensitiveDataFailsValidation` |
| PRIV-BASIS-004 | AC1 | `DeclaredProcessingTests.PRIV_BASIS_004_AC1_NoLibrarySourceNamesABasisWhosePropertiesAreAllUnset` |
| PRIV-SENS-001 | AC1, AC2 | `DeclaredProcessingTests.PRIV_SENS_001_AC1_SensitivityIsACategoryOfTheDeclaredList`, `ProcessingRecordsTests.PRIV_SENS_001_AC2_SensitivityIsAColumnOfItsOwnAsync` |
| PRIV-SENS-002 | AC1, AC2, AC3, AC4 | `ConsentGateTests.PRIV_SENS_002_AC1_AConsentBasedPurposeWithoutAConsentIsRefusedAsync`, `ConsentGateTests.PRIV_SENS_002_AC1_AWrittenConsentAdmitsTheActionAsync`, `ConsentGateTests.PRIV_SENS_002_AC1_AnOrdinaryConsentOverASensitiveTypeIsRefusedAsync`, `ErasureTests.PRIV_SENS_002_AC2_ADumpOfTheSchemaHoldsNoPersonalFieldAsync`, `ErasureTests.PRIV_SENS_002_AC3_AFieldDeclaredForFilteringIsStillQueriedInSqlAsync`, `ProcessingRecordsTests.PRIV_SENS_002_AC4_TheMeasuresAreNamedOneAThreatAsync` |
| PRIV-SENS-002a | AC1, AC2, AC3, AC4 | `ConsentGateTests.PRIV_SENS_002a_AC1_AnotherPurposeOnTheSameRecordIsUntouchedAsync`, `ConsentGateTests.PRIV_SENS_002a_AC2_WithdrawingStopsThePurposeOnTheNextRequestAsync`, `ConsentTests.PRIV_SENS_002a_AC4_APurposeOnAnotherBasisTakesNoConsentAsync` |
| PRIV-SENS-003 | AC1, AC2, AC3 | `ConsentGateTests.PRIV_SENS_003_AC1_TheSensitiveDeclarationDerivesTheWrittenRequirementAsync`, `ConsentGateTests.PRIV_SENS_003_AC2_TheWrittenRecordIsAskedForByTheConsentBasedPurposeOnlyAsync`, `ConsentGateTests.PRIV_SENS_003_AC3_TheRecordIsReadForACustomerWhoConsentedToNothingAsync` |
| PRIV-SENS-004 | AC1, AC2 | `PrivacyContractTests.PRIV_SENS_004_AC1_NoLibraryFieldOrSourceNamesCardData` |
| PRIV-CONS-001 | AC1, AC2, AC3 | `ConsentStoreTests.PRIV_CONS_001_AC1_AConsentReadsBackEveryFieldItWasWrittenWithAsync`, `ConsentTests.PRIV_CONS_001_AC1_ConsentForTwoPurposesProducesTwoRecordsAsync`, `ConsentTests.PRIV_CONS_001_AC1_EveryChangeIsAuditedByCodeAsync`, `ConsentTests.PRIV_CONS_001_AC1_TheRecordCarriesTheMechanismAsync`, `ConsentTests.PRIV_CONS_001_AC2_TheRecordNamesTheNoticeVersionDisplayedAsync`, `ConsentTests.PRIV_CONS_001_AC3_WithdrawalSetsATimestampRatherThanDeletingAsync`, `RegistrationServiceTests.PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync` |
| PRIV-CONS-002 | AC1, AC2 | `PrivacyContractTests.PRIV_CONS_002_AC1_NoConsentOperationTakesMoreThanOnePurpose`, `RegistrationServiceTests.PRIV_CONS_002_AC1_EachTickedControlIsItsOwnRecordAsync` |
| PRIV-CONS-003 | AC1, AC2 | `RegistrationServiceTests.PRIV_CONS_003_AC1_NoConsentIsRecordedForAControlLeftUntickedAsync` |
| PRIV-CONS-004 | AC1, AC2 | `ConsentTests.PRIV_CONS_004_AC1_TheWrittenRecordIsDistinguishableFromTheOrdinaryAsync`, `ConsentTests.PRIV_CONS_004_AC2_TheWrittenRecordIsRetrievableAfterwardsAsync` |
| PRIV-CONS-005 | AC1, AC2, AC3, AC4 | `LegalDocumentEndpointTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`, `LegalDocumentEndpointTests.PRIV_CONS_005_AC3_TheGoverningTextAndTheTranslationsComeBackTogetherAsync`, `LegalDocumentEndpointTests.PRIV_CONS_005_AC4_AnotherDocumentIsServedTheSameWayAsync`, `LegalDocumentEndpointTests.PRIV_CONS_005_AC4_TheNoticeCarriesItsGoverningLanguageAsync`, `LegalDocumentStoreTests.PRIV_CONS_005_AC1_AVersionComesBackWithItsGoverningLanguageAsync`, `LegalDocumentTests.PRIV_CONS_005_AC1_AVersionCarriesExactlyOneGoverningLanguageAsync`, `LegalDocumentTests.PRIV_CONS_005_AC1_AVersionWithoutAGoverningLanguageTakesTheConfiguredOneAsync`, `LegalDocumentTests.PRIV_CONS_005_AC1_AnUnpublishedDocumentIsRefusedAsync`, `LegalDocumentTests.PRIV_CONS_005_AC2_ATranslationAttachesWithoutCreatingAVersionAsync`, `LegalDocumentTests.PRIV_CONS_005_AC2_AVersionPublishesWithNoTranslationAttachedAsync`, `LegalDocumentTests.PRIV_CONS_005_AC3_TheGoverningTextAndItsTranslationsComeBackTogetherAsync` |
| PRIV-CONS-006 | AC1, AC2, AC3 | `LegalDocumentEndpointTests.PRIV_CONS_006_AC2_ANamedVersionResolvesToTheTextItCarriedAsync`, `LegalDocumentStoreTests.PRIV_CONS_006_AC1_ATranslationAttachesToTheVersionItCorrectsAsync`, `LegalDocumentStoreTests.PRIV_CONS_006_AC1_AnUnpublishedVersionTakesNoTranslationAsync`, `LegalDocumentStoreTests.PRIV_CONS_006_AC1_TheCurrentVersionIsTheLastPublishedAsync`, `LegalDocumentTests.PRIV_CONS_006_AC1_APublicationWritesAndCommitsOnceAsync`, `LegalDocumentTests.PRIV_CONS_006_AC1_ChangingTheGoverningTextCreatesANewVersionAsync`, `LegalDocumentTests.PRIV_CONS_006_AC1_CorrectingATranslationCreatesNoVersionAsync`, `LegalDocumentTests.PRIV_CONS_006_AC3_AVersionWithoutGoverningTextIsRefusedAndRaisedAsync` |
| PRIV-CONS-006a | AC3 | `PrivacyContractTests.PRIV_CONS_006a_AC3_TheLibrarySetsOnlyTheFiveNecessaryCookies` |
| PRIV-CONS-007 | AC1, AC2, AC3, AC4 | `ConsentGateTests.PRIV_CONS_007_AC4_ASupersededConsentIsRefusedAsSupersededAsync`, `ConsentStoreTests.PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync`, `LegalDocumentTests.PRIV_CONS_007_AC1_TheAuditRecordCarriesTheAnswerOnMaterialityAsync`, `SupersessionTests.PRIV_CONS_007_AC1_AConsentAgainstTheCurrentVersionSurvivesAsync`, `SupersessionTests.PRIV_CONS_007_AC1_AMaterialChangeIdentifiesWhoMustBeAskedAgainAsync`, `SupersessionTests.PRIV_CONS_007_AC1_ThePublicationRecordsHowManyConsentsItEndedAsync`, `SupersessionTests.PRIV_CONS_007_AC2_AMaterialRevisionOfAnotherDocumentEndsNoConsentAsync`, `SupersessionTests.PRIV_CONS_007_AC2_OnlyTheConsentBasedPurposesAreSuspendedAsync`, `SupersessionTests.PRIV_CONS_007_AC3_AnImmaterialRevisionTouchesNoConsentAsync`, `SupersessionTests.PRIV_CONS_007_AC4_ASupersededConsentPromptsRatherThanWithdrawsAsync` |
| PRIV-CONS-008 | AC1, AC2, AC3, AC4 | `ConsentEndpointTests.PRIV_CONS_008_AC1_WithdrawingTakesTheOneRequestGrantingTookAsync`, `ConsentStoreTests.PRIV_CONS_008_AC4_WithdrawalKeepsTheRecordAndTimestampsItAsync`, `ConsentTests.PRIV_CONS_008_AC1_WithdrawalTakesNoMoreInteractionsThanGrantingAsync`, `ConsentTests.PRIV_CONS_008_AC4_WithdrawalAnnouncesTheChangeForThePurposeAsync` |
| PRIV-CONS-008a | AC2, AC3 | `ConsentEndpointTests.PRIV_CONS_008a_AC3_APurposeOnAnotherBasisTakesNoConsentAsync`, `RegistrationServiceTests.PRIV_CONS_008a_AC2_ThePresentationRecordCarriesTheVersionAndTheInstantAsync` |
| PRIV-CONS-009 | AC1 | `PrivacyContractTests.PRIV_CONS_009_AC1_TheWithdrawalIsOnTheSameContractAsTheGrant` |
| PRIV-CONS-010 | AC1, AC2 | `PrivacyContractTests.PRIV_CONS_010_AC1_NoLibrarySourceNamesATransferPurpose` |
| PRIV-CONS-011 | AC1, AC2, AC3 | `ConsentEndpointTests.PRIV_CONS_011_AC1_ABrowserWithNoSessionReadsNobodysRecordsAsync`, `ConsentEndpointTests.PRIV_CONS_011_AC1_EveryConsentHeldIsVisibleToItsSubjectAsync`, `ConsentTests.PRIV_CONS_011_AC1_ACallerWithNoSubjectReadsNothingAsync`, `ConsentTests.PRIV_CONS_011_AC1_EveryRecordHeldIsVisibleToItsSubjectAsync` |
| PRIV-RIGHT-001 | AC1, AC2, AC3 | `DeadlineSweepTests.PRIV_RIGHT_001_AC3_ALapseIsAuditedAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC1_ErasureIsNotARequestTypeOnThisEndpointAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC2_AnAuthorisedHumanEntersAnOutOfBandRequestAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC2_TheQueueIsRefusedToACustomerAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_001_AC3_TheDecisionIsRecordedWithItsReasonAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_001_AC1_AnOpenRequestOfATypeIsReadBackAsADuplicateAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC1_ASecondOpenRequestOfATypeIsADuplicateAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC1_ErasureIsNotSubmittedOnTheQueueAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC1_TheSubjectSubmitsWithoutAHumanAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_AFulfilledErasureEntersTheGraceWindowAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_AnOutOfBandRequestRecordsItsIdentityConfirmationAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_EnteringWithoutThePermissionIsRefusedAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC2_TheQueueIsReadByTheHumanWhoWorksItAsync`, `PrivacyRequestTests.PRIV_RIGHT_001_AC3_EveryExerciseIsAuditedAsync` |
| PRIV-RIGHT-001a | AC1, AC2, AC3, AC4, AC5 | `ConsentEndpointTests.PRIV_RIGHT_001a_AC1_APurposeOnANonObjectableBasisIsRefusedAsync`, `ConsentEndpointTests.PRIV_RIGHT_001a_AC1_AnObjectionIsRecordedAndWithdrawnFromTheDashboardAsync`, `ConsentStoreTests.PRIV_RIGHT_001a_AC1_AnObjectionReadsBackAndItsWithdrawalIsTimestampedAsync`, `ConsentTests.PRIV_RIGHT_001a_AC2_ObjectingWritesARecordAndAnnouncesItAsync`, `ConsentTests.PRIV_RIGHT_001a_AC4_WithdrawingAnObjectionTakesEffectWithoutApprovalAsync`, `ConsentTests.PRIV_RIGHT_001a_AC5_ANonObjectablePurposeIsRefusedAsync`, `HandlerCoverageTests.PRIV_RIGHT_001a_AC3_APurposeOnANonObjectableBasisNeedsNoHandler`, `HandlerCoverageTests.PRIV_RIGHT_001a_AC3_AnObjectablePurposeWithNoRegisteredHandlerFailsStartup` |
| PRIV-RIGHT-002 | AC1, AC2, AC3, AC4, AC5 | `DeadlineSweepTests.PRIV_RIGHT_002_AC2_TheHighAlertFiresOnTheDeadlineDayAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC2_TheNormalAlertFiresTwoWorkingDaysBeforeAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC2_TheNormalAlertIsRaisedOnceAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC3_ARestrictionUndecidedAtTheDeadlineIsGrantedAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC4_AnErasureUndecidedAtTheDeadlineIsDeemedRefusedAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC4_TheLapseOfAnErasureErasesNothingAsync`, `DeadlineSweepTests.PRIV_RIGHT_002_AC5_ADecisionBeforeTheDeadlineCancelsBothAlertsAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_002_AC1_ADateLaterThanTodayIsRefusedAsync`, `PrivacyRequestEndpointTests.PRIV_RIGHT_002_AC1_ASubmittedRequestIsAnsweredWithItsReceiptAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_002_AC1_ARequestReadsBackEveryFieldItWasWrittenWithAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_002_AC4_ADecidedRequestKeepsItsRowAsync`, `PrivacyRequestStoreTests.PRIV_RIGHT_002_AC5_ADecidedRequestIsNotReachedByTheSweepAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC1_ADateLaterThanTodayIsRefusedAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC1_ARequestCarriesItsCreationDeadlineAndReceiptAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC1_TheClockRunsFromTheDateReceivedAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC1_TheSubjectIsSentAReceiptOnEntryAsync`, `PrivacyRequestTests.PRIV_RIGHT_002_AC5_ARequestIsDecidedOnceAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC1_AListedHolidayPushesTheDeadlineLaterAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC1_AZoneTheMachineDoesNotKnowIsRefusedAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC1_TheCalendarDayIsTheOneInTheDeploymentZoneAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC1_TheDeadlineIsTheEndOfTheSixthWorkingDayAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC1_TheDeclaredWeekIsTheWeekCountedAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC2_TheEscalationIsMidnightOnTheDeadlineDayAsync`, `WorkingCalendarTests.PRIV_RIGHT_002_AC2_TheWarningIsTwoWorkingDaysBeforeTheDeadlineAsync` |
| PRIV-RIGHT-003 | AC1, AC2, AC3 | `ExportEndpointTests.PRIV_RIGHT_003_AC1_BothFormatsContainTheSameDataAsync`, `ExportEndpointTests.PRIV_RIGHT_003_AC2_ThePortableFormatIsFlatAndStableNamedAsync`, `ExportSourceTests.PRIV_RIGHT_003_AC3_TheExportCarriesThePreferencesIdentifiersAndSessionsAsync` |
| PRIV-RIGHT-004 | AC1, AC2, AC3 | `GateBehaviourTests.PRIV_RIGHT_004_AC2_LiftingTheRestrictionRestoresWhatWasThereBeforeAsync`, `GateBehaviourTests.PRIV_RIGHT_004_AC3_EveryWayOfAskingTheGateCarriesTheRestrictionAsync`, `PrivacyRequestTests.PRIV_RIGHT_004_AC1_FulfillingARestrictionRestrictsTheAccountAsync` |
| PRIV-RIGHT-005 | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | `SubjectEraserTests.PRIV_RIGHT_005_AC1_RecordsKeptUnderTheirOwnBasisAreUntouchedAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC2_TheAuditTrailSurvivesTheErasureAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC3_TheIdentifierIsNeverReissuedAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC4_ThePhotoIsRenderedUnreadableByTheSameOperationAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC5_CountsAndTotalsAreUnchangedByAnErasureAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC7_TheUsernameIsHeldAndThenClaimableAsync` |
| PRIV-RIGHT-005a | AC1, AC2, AC3, AC4, AC5, AC6, AC7, AC8, AC9, AC10, AC11, AC12, AC13 | `AuditStoreTests.PRIV_RIGHT_005a_AC11_AnAggregateOverPlainColumnsIsUnaffectedAsync`, `AuthorizationModelTests.PRIV_RIGHT_005a_AC1_AnEncryptedFieldNamingNoSubjectColumnFailsStartup`, `AuthorizationModelTests.PRIV_RIGHT_005a_AC2_ASubjectColumnNamingNoSubjectFailsStartup`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC12_ReadingAnAccountsIdentifiersUnwrapsItsKeyOnceAsync`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC8_NoFormOfAnIdentifierIsInTheDatabaseInPlainAsync`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC9_TheIdentifiersOfAnErasedSubjectAreNotReadableAsync`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC10_AValueMovedElsewhereDoesNotDecrypt`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC10_AValueMovedToAnotherSubjectDoesNotDecrypt`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC13_ARotationReWrapsTheKeyAndChangesNoStoredValue`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC3_TwoFieldsOfOneRowMayNameDifferentSubjects`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC5_TwoSubjectsWithOneValueProduceDifferentCiphertext`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC6_AnAlteredMarkerFailsRatherThanSelectingAnAlgorithm`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC7_AValueOfTheEarlierSchemeReadsBesideALaterOne`, `PersonalFieldCipherTests.PRIV_RIGHT_005a_AC9_AnErasedWrappedKeyNeverUnwraps`, `PreferenceStoreTests.PRIV_RIGHT_005a_AC8_TheDeclaredValuesAreNotInTheDatabaseInPlainAsync`, `PreferenceStoreTests.PRIV_RIGHT_005a_AC9_TheValuesOfAnErasedSubjectAreNotReadableAsync`, `ProfilePhotoStoreTests.PRIV_RIGHT_005a_AC8_ThePhotoIsNotInTheDatabaseInPlainAsync`, `ProfilePhotoStoreTests.PRIV_RIGHT_005a_AC9_ThePhotoOfAnErasedSubjectIsNotReadableAsync`, `ProfileStoreTests.PRIV_RIGHT_005a_AC8_NoProfileFieldIsInTheDatabaseInPlainAsync`, `ProfileStoreTests.PRIV_RIGHT_005a_AC9_TheProfileOfAnErasedSubjectIsNotReadableAsync`, `SchemaTests.PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync`, `SubjectKeyStoreTests.PRIV_RIGHT_005a_AC13_AReWrappedKeyReadsTheValuesWrittenBeforeItAsync`, `SubjectKeyStoreTests.PRIV_RIGHT_005a_AC9_AnErasedKeyLeavesItsFieldsUnrecoverableAsync`, `SubjectKeyTests.PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey` |
| PRIV-RIGHT-005c | AC1, AC2, AC3, AC4, AC5 | `ErasureTests.PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync`, `ErasureTests.PRIV_RIGHT_005c_AC5_NeutralisedFingerprintsDoNotCollideAsync`, `FingerprintTests.PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints`, `FingerprintTests.PRIV_RIGHT_005c_AC3_TwoCasingsAndTwoCompositionsGiveOneFingerprint`, `FingerprintTests.PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC1_DuplicateDetectionMatchesOnTheKeyedFingerprintAsync`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC4_TheCanonicalisationVersionIsStoredBesideTheFingerprintAsync`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC5_ANeutralisedFingerprintBelongsToNobodyAsync` |
| PRIV-RIGHT-005b | AC1, AC2, AC3, AC4, AC5 | `HandlerCoverageTests.PRIV_RIGHT_005b_AC3_ASensitiveTypeASubscriberCoversStarts`, `HandlerCoverageTests.PRIV_RIGHT_005b_AC3_ASensitiveTypeWithNoRegisteredHandlerFailsStartup`, `HandlerCoverageTests.PRIV_RIGHT_005b_AC3_ATypeThatIsNotSensitiveNeedsNoHandler`, `ModelTests.PRIV_RIGHT_005b_AC4_NoWriteOfTheLibraryReachesAHostTable`, `OutboxPublisherTests.PRIV_RIGHT_005b_AC1_TheDeliveryStaysOpenUntilEveryRequiredSubscriberConfirmsAsync`, `OutboxPublisherTests.PRIV_RIGHT_005b_AC2_ASubscriberThatFaultsIsRetriedRatherThanConfirmedAsync`, `OutboxPublisherTests.PRIV_RIGHT_005b_AC2_AnUnansweredDeliveryRetriesRatherThanBeingMarkedDoneAsync`, `StartupValidationTests.PRIV_RIGHT_005b_AC3_ADeploymentWithNoHandlerForItsSensitiveTypeIsRefusedAsync`, `SubjectEraserTests.PRIV_RIGHT_005b_AC5_OneErasureReachesThePhotoAndEveryOtherStoreAsync` |
| PRIV-RIGHT-006 | AC1 | `PrivacyContractTests.PRIV_RIGHT_006_AC1_NoAnswerTheLibraryGivesCarriesASentence` |
| PRIV-ROPA-001 | AC1, AC2, AC3 | `ComplianceStoreTests.PRIV_ROPA_001_AC2_TheSuppliedFieldsAreReadBackAndASecondStatementReplacesThemAsync`, `ProcessingRecordsEndpointTests.PRIV_ROPA_001_AC1_TheGeneratedOutputMatchesTheTemplatesFieldSetAndOrderingAsync`, `ProcessingRecordsEndpointTests.PRIV_ROPA_001_AC2_TheThreeSuppliedFieldsAreStatedOverTheEndpointAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_AC1_ADeploymentAdmittingMinorsIsInTheChildrensColumnAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_AC1_TheRolesWithAccessAreTheOnesHoldingAServingPermissionAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_AC2_TheThreeSuppliedFieldsAreFlaggedWhileAbsentAsync`, `ProcessingRecordsTests.PRIV_ROPA_001_AC3_APurposeLackingItsAssessmentIsReportedAsync` |
| PRIV-ROPA-002 | AC1, AC2 | `ProcessingRecordsTests.PRIV_ROPA_002_AC1_EveryRecipientAppearsAndAProcessorWithoutAnAgreementIsFlaggedAsync` |
| PRIV-ROPA-003 | AC1 | `ProcessingRecordsTests.PRIV_ROPA_003_AC1_ThePasswordScreeningCallAppearsAsACrossBorderTransferAsync` |
| PRIV-BREACH-001 | AC2, AC3, AC4 | `ProcedureDocumentTests.PRIV_BREACH_001_AC2_TheClockStartsAtDetectionAndTheTimelineIsRecorded`, `ProcedureDocumentTests.PRIV_BREACH_001_AC3_NoProcedureDefersTheClockPendingAssessment`, `ProcedureDocumentTests.PRIV_BREACH_001_AC4_TheOperatorDocumentsStateTheDetectionClock` |
| PRIV-BREACH-002 | AC1, AC2 | `AuditStoreTests.PRIV_BREACH_002_AC1_TheReadBySubjectTakesTheIndexAndNotAScanAsync`, `AuditStoreTests.PRIV_BREACH_002_AC2_TheReadAnswersAfterErasureWithTheRecordsAnonymisedAsync` |
| PRIV-BREACH-003 | AC1 | `ProcedureDocumentTests.PRIV_BREACH_003_AC1_TheProcedureStatesTheNoticeIsNonSuppressible` |
| PRIV-RET-001 | AC1, AC2, AC3 | `ConfigurationCoverageTests.PRIV_RET_001_AC1_ADeclaredCategoryWithNoPeriodFailsStartupAsync`, `ConfigurationCoverageTests.PRIV_RET_001_AC1_EveryDeclaredCategoryWithAPeriodStartsAsync`, `ProcessingRecordsTests.PRIV_RET_001_AC3_TheRetentionOfEachCategoryIsOnTheRowAsync`, `SettingsTests.PRIV_RET_001_AC2_ARetentionBelowTheCategorysFloorIsRejected` |
| PRIV-RET-002 | AC1, AC2, AC3, AC4, AC5 | `AuditRecordTests.PRIV_RET_002_AC1_TheAuditPortOffersNoWriteButAnAppend`, `AuditStoreTests.PRIV_RET_002_AC1_TwoEventsOnOneSubjectBothStandAsync`, `AuditStoreTests.PRIV_RET_002_AC2_NoAttributeIsInTheRowInPlainAsync`, `AuditStoreTests.PRIV_RET_002_AC3_TheSweepKeepsThreeMonthsOpenPerCategoryAsync`, `AuditStoreTests.PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync`, `DatabaseRoleTests.PRIV_RET_002_AC1_TheApplicationCannotUpdateOrDeleteAnAuditRowAsync`, `DatabaseRoleTests.PRIV_RET_002_AC3_AnExpiredPartitionIsDroppedAndTheOthersStandAsync`, `DatabaseRoleTests.PRIV_RET_002_AC5_OnlyTheMaintenanceRoleExecutesTheDropAsync` |
| PRIV-RET-003 | AC1, AC2 | `AuditStoreTests.PRIV_RET_003_AC1_TheRecordedInstantIsWhenTheEventOccurredAsync` |
| PRIV-RET-004 | AC1 | `AuditRecordTests.PRIV_RET_004_AC1_WhatHappenedIsACodeAndNotASentence`, `AuditStoreTests.PRIV_RET_004_AC1_TheRowHoldsCodesAndStructuredFieldsAsync` |
| PRIV-RET-005 | AC1, AC2, AC3 | `ProcessingRecordsTests.PRIV_RET_005_AC3_TheTwoRecordsAppearUnderTheDeclaredPurposesAsync`, `SendLedgerTests.PRIV_RET_005_AC2_TheRecordLivesAtMostTheLongestBucketIntervalAsync`, `SessionStoreTests.PRIV_RET_005_AC1_TheLocationIsUnreadableAfterErasureAndGoneWithTheSessionAsync` |
| PRIV-MINOR-001 | AC1, AC2, AC3, AC4 | `ConfigurationCoverageTests.PRIV_MINOR_001_AC4_ADeploymentOpenToMinorsWithNoWrittenConsentBasisFailsStartupAsync`, `ConfigurationCoverageTests.PRIV_MINOR_001_AC4_AWrittenConsentBasisOrAnAdultOnlyServiceStartsAsync`, `RegistrationServiceTests.PRIV_MINOR_001_AC1_NoAccountExistsWithoutTheDerivedAffirmationAsync`, `RegistrationServiceTests.PRIV_MINOR_001_AC2_TheDateIsKeptWhereTheDeploymentKeepsItAsync`, `RegistrationServiceTests.PRIV_MINOR_001_AC2_TheDateIsNotKeptWhereTheDeploymentDoesNotKeepItAsync` |
| PRIV-MINOR-002 | AC1 | `ProcedureDocumentTests.PRIV_MINOR_002_AC1_TheTakedownProcedureExists` |
| PRIV-MIN-001 | AC1, AC2 | `PrivacyContractTests.PRIV_MIN_001_AC1_NoLibrarySourceNamesAnOrdersContents` |
| PRIV-MIN-002 | AC1, AC2, AC3 | `DeliveryReportsTests.PRIV_MIN_002_AC1_TheCallbackEndpointIsUnguessableCountedAndInertAsync` |
| AUTH-SESS-010 | AC1, AC3 | `AccountLifecycleTests.AUTH_SESS_010_AC1_DeactivationEndsTheAccountsSessionsAsync` |
| AUTHZ-GATE-005 | AC1, AC2, AC3 | `ConsentGateTests.AUTHZ_GATE_005_AC3_ACapabilityCarriesTheConsentItStillRequiresAsync` |
| AUTHZ-MODEL-003 | AC1, AC2 | `DeclaredProcessingTests.AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor` |
| IDN-ACCT-007 | AC1, AC2, AC3, AC4 | `AccountLifecycleFlowTests.IDN_ACCT_007_AC4_AWindowThatHasRunOutIsRefusedAsync`, `AccountLifecycleFlowTests.IDN_ACCT_007_AC4_TheLinkEndsTheWindowAsync`, `AccountLifecycleTests.IDN_ACCT_007_AC4_AWindowThatHasRunOutCancelsNothingAsync`, `AccountLifecycleTests.IDN_ACCT_007_AC4_TheLinkCancelsThroughoutTheWindowAsync`, `DeletionSweepTests.IDN_ACCT_007_AC4_ACancelledWindowIsNotReachedByThePassAsync` |
| IDN-ATTR-001 | AC1 | `ExportSourceTests.IDN_ATTR_001_AC1_TheLanguagePreferenceIsInTheExportAsync` |
| IDN-LIFE-003a | AC2, AC3, AC4, AC6 | `HandlerCoverageTests.IDN_LIFE_003a_AC6_AMissingHandlerFailsStartup`, `OutboxPublisherTests.IDN_LIFE_003a_AC3_ASubscriberThatConfirmedIsNotOfferedTheEventAgainAsync`, `OutboxPublisherTests.IDN_LIFE_003a_AC4_ASpentRetryBudgetAlertsImmediatelyAsync`, `OutboxPublisherTests.IDN_LIFE_003a_AC6_ASubscriberTheLibraryNeverNamedReceivesTheEventAsync`, `SubjectEventContractTests.IDN_LIFE_003a_AC2_NoSubjectEventCarriesATypeAHostDeclared`, `SubjectEventContractTests.IDN_LIFE_003a_AC2_NoSubjectEventNamesAConsumerOrItsDomain` |
| IDN-LIFE-013 | AC1 | `AccountLifecycleFlowTests.IDN_LIFE_013_AC1_TheNoticesLinkStandsTheAccountBackUpAsync`, `AccountLifecycleTests.IDN_LIFE_013_AC1_TheLinkRestoresTheAccountAndWhatItHeldAsync` |
| IDN-LIFE-014 | AC1, AC2, AC3 | `SubjectEraserTests.IDN_LIFE_014_AC1_NoPersonalAttributeIsRetrievableAfterDeletionAsync` |
| IDN-ORG-005 | AC1, AC2 | `SubjectEraserTests.IDN_ORG_005_AC1_TheOrganizationsTrailSurvivesItsErasureAsync` |
| LIB-API-005 | AC1 | `LegalDocumentTests.LIB_API_005_AC1_PublishingWithoutThePermissionIsRefusedAsync`, `LegalDocumentTests.LIB_API_005_AC1_TranslatingWithoutThePermissionIsRefusedAsync` |
| REG-ACCT-001 | AC1, AC2, AC3 | `SubjectEraserTests.REG_ACCT_001_AC3_EveryKeyFieldIsUnreadableAndTheRestIsAsChapterFourSaysAsync` |
| REG-PREF-001 | AC1, AC2, AC3, AC4 | `ExportSourceTests.REG_PREF_001_AC4_TheExportCarriesThePreferencesAndErasureTakesTheValuesAsync` |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| PRIV-CONS-006a AC1, AC2 | The notice that lists the cookies with their purpose and lifetime, and the absence of a prompt while only the necessary ones are set, are pages. The library sets five cookies and no sixth (AC3) and writes no sentence (CONV-CONTENT-001) | The frontend, Milestone 2 step 10 |
| PRIV-CONS-008a AC1 | Whether an interface offers an acceptance control is the interface's. What the library records is a presentation, which AC2 pins | The frontend, Milestone 2 step 10 |
| PRIV-BREACH-001 AC1 | The notification templates for the two audiences are operator documents; no user-facing wording is decided in library code. `11-runbook` section 8.3 carries the procedure for both audiences, which AC2 to AC4 pin | Milestone 2 |
| PRIV-RET-004 AC2 | The audit fields are codes and structured values (AC1); rendering them happens at display | The frontend, Milestone 2 step 10 |
| PRIV-RIGHT-005c AC6 | A negative claim about what a host application has to maintain, which phase 1 restated for when the host-side work exists | Milestone 2 |
| PRIV-MIN-001 AC3 | The outbound payload is the host's shipping integration; the library holds no shipping provider, and that nothing of an order's contents is reachable in it is AC1 and AC2 | Milestone 2 |
| PRIV-MINOR-002 AC2 | The operation that performs suspension, cancellation, erasure and recording with per-subscriber completion is the takedown; the outbox it runs on is built here (IDN-LIFE-003a) | Phase 8 |
| IDN-LIFE-003 AC2, AC3 | The same takedown operation and the audit line it writes | Phase 8 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `ConfigurationInMemory` in `Janus.Privacy.Tests`, `Janus.Authentication.Tests` and `Janus.Hosting.Tests` | The fakes answered a required key the deployment never named with the key's default, which the real store never does, so a test could not reach the undeclared path at all | LIB-HOST-001 with `10` section 4, which the real `ConfigurationStore` implements | A required key nobody named is undeclared in the fake as it is in the store: the read fails with `model.startup.declarationmissing` |
| `Janus.Privacy.Tests.csproj`, `Janus.Hosting.Tests.csproj` | The endpoints of `09` section 7 are tested at the browser boundary, and the fakes the privacy area is tested against were not reachable from there | the working guide's section 3, test infrastructure | The privacy test project grants `InternalsVisibleTo` to the hosting test project and the hosting test project references it, so one set of fakes serves both; no shipped project's surface changes |
| `ErrorCodesTests.Catalogue`, `VocabularyContractTests` | The frozen catalogue and the frozen wire-name list predated this phase's codes and message kinds | `10` section 1 and `10` section 5.19, which name them | The lists are the vocabulary as this phase leaves it, so the eight `privacy.*` codes, the four `identity.*` codes of deactivation, deletion, reactivation and takedown, and the four message kinds join them |
| `ModelTests` | The frozen column list predated the tables this phase adds | OPS-DATA-001, which the list stands for | The list is the schema as this phase leaves it, so the consent, objection, document, request, outbox, erasure and compliance columns join it |
| `PublicSurfaceTests.ModelBuilder` | The test read `AuthorizationModel.cs` as a shipped file using reflection, and the model builder is the one place CONV-CODE-004 admits it | CONV-CODE-004 AC2, whose text is "outside the model builder and tests" | The file the rule names is on the list beside the two files the builder already used |
| `FailClosedTests.Catches` | The scanner took every occurrence of the text `catch` for a catch clause, so the word "catches" in a comment made the whole file's first block read as one that allows on an exception | AUTH-PRIN-001 AC3, the working guide's section 3, a gate that mis-implements its own rule | A clause is an occurrence followed by the exception it takes or the block it opens, so the scanner checks what the item says |
| `ConsentStoreTests.PRIV_CONS_007_AC1_OnlyLiveConsentsAgainstAnEarlierVersionAreFoundAsync` | The read is over every subject the store holds, and the assertion was over all of it, so a live consent another test of the shared database had left made it fail | PRIV-CONS-007 AC1 | The assertion is over the three subjects the test registers, so what it counts is its own live consent against an earlier version and nothing else |
| `HostFixture` | The deployment under test declares two data categories and named a retention period for neither, so it could not start once the check of `10` section 4.7 existed | PRIV-RET-001 with `10` section 4.7 | The fixture names `retention.identity` and `retention.history`, as a deployment declaring those categories must |

## 4. Decided in the owner's absence

Under D-161. Each entry is in `docs/reports/decisions-pending-review.md` in the same
words, numbered as it is there.

| # | Decision |
|---|---|
| 77 | A purpose declares the data and subject categories it requires |
| 78 | The capture path is derived from the basis and the sensitivity |
| 79 | What the deployment processes is read from Core |
| 80 | The privacy area raises its alerts through its own port |
| 81 | A document version is numbered by how many came before it |
| 82 | Supersession is keyed to the privacy notice |
| 83 | A refusal `10` gives no code for is refused as denied |
| 84 | A consent record carries when it was superseded |
| 85 | The consent event names which way the consent changed |
| 86 | Privacy records are audited under the security category |
| 87 | The dashboard records the mechanism it is |
| 88 | An action is bound to the purpose it is done for |
| 89 | The consent the gate reads is the caller's own |
| 90 | A consent control for a purpose taking no consent refuses the terms step |
| 91 | The sweep that fires a deadline is built here and scheduled in phase 9 |
| 92 | The receipt and the lapse notice are two new message kinds |
| 93 | A request the caller may not decide is refused as denied, whatever the reason |
| 94 | A rectification that lapses is deemed refused, as an erasure is |
| 95 | The request and the delivery carry typed identifiers |
| 96 | A handler names what it covers, and the check reads the declaration against it |
| 97 | A handler that faults is a handler that did not confirm |
| 98 | The link-borne bodies carry `linkToken`, including reactivation |
| 99 | The export asks for step-up through a Core port the authentication area implements |
| 100 | The export rate limit counts over a rolling day, and the refusal names the instant it lifts |
| 101 | A request naming no format, or one the chapter does not name, is malformed |
| 102 | The export carries every group of REG-ACCT-001 the account may see, not only the three PRIV-RIGHT-003 names |
| 103 | The provider register of chapter 05 section 8 ships as a default the host takes, and a row that names no location follows the hosting |
| 104 | The children's column is true for every row exactly when the deployment admits minors |
| 105 | The three supplied fields are one replaceable row, and the register is generated only for `format=template` |
| 106 | The retention cell is one entry a data category, longest first, and a category with no key is flagged |
| 107 | A record whose subject key is destroyed is read anonymised, not refused |
| 108 | A declared data category with no retention key stops the deployment |
| 109 | A subject column is one the declared type holds and that holds a subject |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of the phase:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 553, `Janus.Authorization.Tests`
111, `Janus.Core.Tests` 429, `Janus.Hosting.Tests` 292, `Janus.Identity.Tests` 62,
`Janus.Privacy.Tests` 128 and `Janus.Storage.Tests` 251, none failing.

Full gate: GitHub Actions runs `35689671635` (push) and `35689674588` (pull request)
on branch `phase-07-privacy`, pull request #15, green on every job. `Integration
tests`, `Double migration run`, `Destructive-operation detection report`,
`Truth-table suite` and `Dependency vulnerability alerting` run on the
pull-request event and `Secret scanning` on the push event, as CONV-GATE-002
states, so the two runs together are one pass of the table of CONV-GATE-001.

The commit after the two runs above changes this section and the status line
alone.
