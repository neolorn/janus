# Phase 9: Operations code

Status: complete on the development machine, no open question. The full gate is green
locally except the commit message job, which fails on one body line of an unpushed
commit (section 5). The pipeline run waits on the branch being pushed. Built in this
phase:

- the bootstrap command: first organization, the first administrator's enrolment
  address, the reserved `emergency` account and the canary subject; it issues no
  break-glass credential, and the missing credential is raised until one is generated;
- the break-glass credential, its generation, its use and its session;
- the rotation commands for the key-encryption key and the fingerprint key, which
  resume after a killed run, print the escrow copy and retire the previous version
  only once the copy is sealed;
- `configure` for protected keys and `register-client` for the client registry, with a
  replaced secret kept through its overlap;
- every raised condition carried to the alert channels, with each condition of chapter
  06 raised from a site in the library and fired from a test;
- the licence and maintenance log, and the warning ahead of the annual operation the
  key is rotated in;
- the background worker, which runs every scheduled job as a named principal: the event
  publisher the library lacked, the send retry, the audit partition upkeep, the
  recovery-code reminder, the location file refresh and the restore test against a
  throwaway instance;
- the off-host erasure ledger writer and its replay after a restore;
- the gating, limit and audit of export operations;
- stage 11 concealment.

Defects of earlier phases found here are fixed, with their tests in section 1 and, where
a decision was needed, their entries in section 4:

- audit records written outside their action's transaction;
- application role grants missing for later tables;
- the pre-authentication store without its keys;
- a host's step-up gate never met (entry 328);
- the recovery-code reminder never sent (entry 335);
- the audit partitions never kept (entry 336);
- a blocklist fallback only logged (entry 337);
- concealment unbuilt (entry 339);
- the client registry never written (entry 340);
- the records of processing never committed;
- the system policy judged a loosening whatever its direction;
- the eraser breaking the held-restriction check.

The decisions taken in the owner's absence are in section 4 and, in the same words, in
`docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTH-ABUSE-004 | the item, AC2 | `SendingServiceTests.AUTH_ABUSE_004_AC2_ARetryIsJudgedByTheRestrictionsAgainAsync`, `SendingServiceTests.AUTH_ABUSE_004_ARestrictionGovernsEverySendWhateverItsNameAsync` |
| AUTH-FACT-008 | AC5 | `AccountServiceTests.AUTH_FACT_008_AC5_TheReadCarriesWhenTheSetWasRemindedOfAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_AnAccountThatIsNotActiveIsNotRemindedAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_AnOldSetRemindsItsOwnerOnceAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_EverySetDueIsRemindedOnceInOnePassAsync`, `RecoveryCodeRemindersTests.AUTH_FACT_008_AC5_TheAgeIsWhatTheDeploymentConfiguresAsync`, `RecoveryCodeStoreTests.AUTH_FACT_008_AC5_TheSetsOwedTheirReminderAreReadOldestFirstAsync` |
| AUTH-KEY-002 | the item, AC2 | `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnARetainedFingerprintKeyShorterThanTheHash`, `KeyMaterialTests.AUTH_KEY_002_EveryStoreIsHandedTheKeysTheHostPassedIn` |
| AUTH-OIDC-001 | the item, AC4 | `ClientRegistryTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredFromTheServerAsync`, `ClientRegistryTests.AUTH_OIDC_001_AClientTheRegistryCannotServeIsRefusedAsync`, `RegisterClientTests.AUTH_OIDC_001_AC4_TheMailServerClientIsRegisteredAsTheDeploymentIsStoodUpAsync`, `RegisterClientTests.AUTH_OIDC_001_ARegistrationWithoutAUsableSecretIsRefusedAsync`, `RegisterClientTests.AUTH_OIDC_001_AnArgumentTheCommandCannotTakeIsRefusedAsync` |
| AUTH-STEP-001 | the item, AC1, AC2 | `GateBehaviourTests.AUTH_STEP_001_AListUnderABoundActionAsksForStepUpAsync`, `StepUpGatesTests.AUTH_STEP_001_AC1_ExercisingABoundPermissionAsksForStepUpAsync`, `StepUpGatesTests.AUTH_STEP_001_AC2_SplittingAnApplicationChangesNothingAsync` |
| AUTH-STEP-002 | the item, AC3 | `GateBehaviourTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync`, `StepUpGatesTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync`, `StepUpGatesTests.AUTH_STEP_002_AnotherPersonsSessionMeetsNoGateAsync`, `StepUpGuardTests.AUTH_STEP_002_AGateNamedInTheCatalogueCostsItsOwnValuesAsync`, `StepUpGuardTests.AUTH_STEP_002_AnotherPersonsSessionIsRefusedAsync` |
| AUTH-STEP-003 | AC1, AC2 | `StepUpGatesTests.AUTH_STEP_003_AC1_WithNoAssuranceProviderABoundPermissionIsDeniedAsync`, `StepUpGatesTests.AUTH_STEP_003_AC2_TheDenialIsDistinguishableFromAnOrdinaryOneAsync` |
| AUTH-STEP-004 | AC2 | `BreakGlassEndpointTests.AUTH_STEP_004_AC2_AnActionInTheSessionIsAuditedAsTheReservedAccountAsync` |
| AUTHZ-CONCEAL-001 | AC1, AC2 | `ExplanationTests.AUTHZ_CONCEAL_001_AC1_ARefusalOnATypeDeclaringNothingIsConcealedAsync`, `ExplanationTests.AUTHZ_CONCEAL_001_AC2_ARefusalOnADisclosingTypeIsNotConcealedAsync` |
| AUTHZ-CONCEAL-004 | AC1 | `ConcealmentTests.AUTHZ_CONCEAL_004_AC1_TheAnswerCarriesTheIdentifierTheRefusalWasRecordedUnderAsync` |
| AUTHZ-GATE-005 | the item | `StepUpGuardTests.AUTHZ_GATE_005_AGateTheHostNamesCostsTheDearestGateOfThePolicyAsync` |
| BFF-ERR-003 | AC1, AC3 | `BrowserProfileTests.BFF_ERR_003_AC3_AnEndpointAnsweringPastAConcealedRefusalIsAnsweredAsAbsenceAsync`, `ConcealmentTests.BFF_ERR_003_AC1_AConcealedRefusalIsTheSameBytesWhateverTheEndpointWroteAsync` |
| CONV-DESIGN-002 | the item | `EventPublisherTests.CONV_DESIGN_002_AnEventReachesEveryConsumerOfItsKindAndIsMarkedAsync`, `PendingEventsTests.CONV_DESIGN_002_AnEventWaitsOnlyOnceItsTransactionCommitsAsync` |
| CONV-DESIGN-005 | AC1 | `AlertChannelsTests.CONV_DESIGN_005_AC1_AnEventTheHostRefusedFailsTheRaiseAsync` |
| D-022 | the item | `SendOutboxTests.D_022_AnAttemptReadsBackAsItWasRecordedAsync`, `SendOutboxTests.D_022_OnlyAMessageWhoseAttemptIsDueIsReadAsync`, `SendingServiceTests.D_022_AMessageWhoseBudgetIsSpentIsRemovedAndRaisesDegradationAsync`, `SendingServiceTests.D_022_ARefusedMessageIsCarriedOnceItsRetryIsDueAsync` |
| DR-006a | AC1, AC2, AC3 | `ErasureReplayTests.DR_006a_AC1_AnErasureTheRestoreTookBackIsCarriedOutAgainAsync`, `LibraryStructureTests.DR_006a_AC2_NoErasureProcedureReachesABackup`, `SubjectEraserTests.DR_006a_AC3_ASubjectErasedBeforeTheBackupIsNotRecoverableFromItAsync` |
| DR-007 | the item, AC1, AC2, AC3, AC4 | `BootstrapTests.DR_007_TheCanarySubjectIsSeededAsync`, `RestoreTestTests.DR_007_AC1_TheTestRunsAtItsIntervalWithoutAPersonAsync`, `RestoreTestTests.DR_007_AC2_TheMeasuredTimeIsRecordedAgainstTheObjectiveAsync`, `RestoreTestTests.DR_007_AC3_ABackupTheLiveKeyCannotOpenIsRaisedAsync`, `RestoreTestTests.DR_007_AC3_ABackupWhoseAccountsTheLiveFingerprintKeyCannotFindIsRaisedAsync`, `RestoreTestTests.DR_007_AC3_ARunThatRestoresNothingIsRaisedAsync`, `RestoreTestTests.DR_007_AC4_TheRestoredCanaryDecryptsAndItsAccountIsFoundAsync` |
| DR-008 | AC1, AC2 | `RestoreTestTests.DR_008_AC1_TheTestReadsTheRestoredInstanceAndLeavesTheRunningOneAsync`, `RestoreTestTests.DR_008_AC2_TheInstanceDoesNotOutliveTheTestAsync` |
| DR-009a | AC1, AC2, AC3, AC4, AC5 | `EnvelopeRotationWatchTests.DR_009a_AC1_AnOperationUndoneOrNeverRecordedIsRaisedAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_AnOperationWithinItsCryptoperiodRaisesNothingAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_TheLeadIsTheConfiguredOneAsync`, `EnvelopeRotationWatchTests.DR_009a_AC1_TheOperationInsideTheLeadIsRaisedAsDueAsync`, `KeyRotationTests.DR_009a_AC2_RotationLeavesEveryCiphertextAsItWasAsync`, `KeyRotationTests.DR_009a_AC3_ARotationRunsOnDemandOutOfCycleAsync`, `KeyRotationTests.DR_009a_AC4_TheOperationThatRotatesReplacesTheEscrowCopyAsync`, `LibraryStructureTests.DR_009a_AC5_NoPathButTheCommandRotatesTheKey` |
| DR-016 | the item, AC2, AC3, AC4 | `AccountTests.DR_016_AC3_AnAccountLeftDeletingKeepsItsDeletion`, `AccountTests.DR_016_AC3_AnErasedOrEmergencyAccountIsNotReapplied`, `AccountTests.DR_016_AC3_AnErasureIsReappliedFromTheStateARestoreLeft`, `ErasureEndpointTests.DR_016_AC2_AManualCompletionIsAFaultWhileTheLedgerCannotTakeTheLineAsync`, `ErasureLedgerLineTests.DR_016_ALineInAnyOtherFormIsNotRead`, `ErasureLedgerLineTests.DR_016_ALineIsReadAsTheErasureItWasWrittenFor`, `ErasureReplayTests.DR_016_AC3_ALedgerWithALineInAnotherFormIsRefusedWholeAsync`, `ErasureReplayTests.DR_016_AC3_AnUnreadableLedgerIsRefusedAsync`, `ErasureReplayTests.DR_016_AC3_TheWholeLedgerIsReplayedAndASecondReplayChangesNothingAsync`, `ErasureServiceTests.DR_016_AC2_ALineAlreadyWrittenIsNotWrittenAgainAsync`, `ErasureServiceTests.DR_016_AC2_AManualCompletionClosesNothingWhileTheLineCannotBeWrittenAsync`, `ErasureServiceTests.DR_016_AC2_AManualCompletionWritesTheLineBeforeItClosesTheErasureAsync`, `ErasureServiceTests.DR_016_AC2_AnErasureIsReadWithItsLedgerLineAsync`, `HandlerCoverageTests.DR_016_AC2_ASubscriberUnderTheLedgersNameFailsStartup`, `OutboxPublisherTests.DR_016_AC2_ALedgerThatNeverTakesTheLineIsRaisedWhenTheBudgetIsSpentAsync`, `OutboxPublisherTests.DR_016_AC2_AnErasureIsNotCompleteUntilItsLineIsDurableAsync`, `OutboxPublisherTests.DR_016_AC4_TheLineHoldsTheInstantTheSubjectAndTheReasonAndNothingElseAsync`, `OutboxPublisherTests.DR_016_OnlyAnErasureWaitsForTheLedgerAsync`, `StartupValidationTests.DR_016_AC2_ASubscriberUnderTheLedgersNameIsRefusedAsync` |
| FE-BG-001 | AC3 | `BreakGlassEndpointTests.FE_BG_001_AC3_AStaleSessionCookieIsIgnoredAsync` |
| IDN-ATTR-001 | the item | `SendingServiceTests.IDN_ATTR_001_ARetryCarriesOnlyTheLanguagesStillOwedAsync` |
| IDN-AUD-001 | AC1 | `AuditStoreTests.IDN_AUD_001_AC1_AnEventInsideATransactionFallsWithItAsync`, `AuditStoreTests.IDN_AUD_001_AC1_AnEventOutsideATransactionIsKeptAsync` |
| IDN-LIFE-003a | the item | `EventPublisherTests.IDN_LIFE_003a_AnEventWhoseBudgetIsSpentFailsAndRaisesDegradationAsync`, `EventPublisherTests.IDN_LIFE_003a_OnlyAConsumerThatRefusedIsOfferedTheEventAgainAsync`, `HandlerCoverageTests.IDN_LIFE_003a_TwoSubscribersUnderOneNameFailStartup`, `PendingEventsTests.IDN_LIFE_003a_AMarkedOrFailedEventIsNotReadAgainAsync` |
| IDN-PRIN-001 | AC4 | `AuditStoreTests.IDN_PRIN_001_AC4_ABackgroundActionIsRecordedWithItsReasonAsync`, `AuditStoreTests.IDN_PRIN_001_AC4_APrincipalIsRecordedOnlyWithItsReasonAndNoActorAsync`, `BootstrapTests.IDN_PRIN_001_AC4_WhatBootstrapDefinesAndSetsIsRecordedUnderItsPrincipalAsync`, `LossReportsTests.IDN_PRIN_001_AC4_AnInvalidationIsRecordedUnderTheSweepAsync` |
| INF-BG-001 | AC1, AC2 | `BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`, `BackgroundWorkerTests.INF_BG_001_AC1_AJobRunsAtItsIntervalWithoutAPersonAsync`, `BackgroundWorkerTests.INF_BG_001_AC1_TheIntervalIsTheConfiguredOneAsync`, `BackgroundWorkerTests.INF_BG_001_AC1_TwoProcessesRunAJobOnceBetweenThemAsync`, `BackgroundWorkerTests.INF_BG_001_AC2_AJobThatKeepsFailingIsRaisedOnceAWindowAsync`, `BackgroundWorkerTests.INF_BG_001_AC2_AJobThatThrowsIsRaisedAndTheOthersKeepRunningAsync`, `BackgroundWorkerTests.INF_BG_001_AC2_OnlyTheJobThatStoppedSucceedingIsRaisedAsync`, `JobRunStoreTests.INF_BG_001_AC1_ARunIsClaimedOnceInItsIntervalAsync`, `JobRunStoreTests.INF_BG_001_AC2_AJobNeverRecordedDoesNotLapseAsync`, `JobRunStoreTests.INF_BG_001_AC2_AJobThatNeverSucceededLapsesFromItsFirstRecordingAsync`, `JobRunStoreTests.INF_BG_001_AC2_AJobWhoseLastSuccessIsTwoIntervalsOldLapsesOnceAWindowAsync` |
| INF-BG-002 | the item, AC1 | `BackgroundWorkerTests.INF_BG_002_AC1_AJobRunsAsItsPrincipalAsync`, `BackgroundWorkerTests.INF_BG_002_EveryScheduledJobIsANamedRestrictedPrincipal`, `DeadlineSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync`, `DeletionSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync`, `LossReportsTests.INF_BG_002_AC1_TheAdvanceNeverRunsAsNobodyAsync`, `OrganizationErasureSweepTests.INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync` |
| INF-HOST-001 | the item, AC2 | `EnvironmentWatchTests.INF_HOST_001_AC2_AnUnmeasuredClockIsRaisedAsADegradationAsync`, `EnvironmentWatchTests.INF_HOST_001_AC2_DriftBeyondToleranceRaisesAnAlertAsync`, `EnvironmentWatchTests.INF_HOST_001_DriftWithinToleranceRaisesNothingAsync`, `EnvironmentWatchTests.INF_HOST_001_TheToleranceFollowsTheCodeDriftAsync` |
| INF-TLS-003 | AC2 | `EnvironmentWatchTests.INF_TLS_003_AC2_ARenewalFailureRaisesAnAlertWithoutAnyoneCheckingAsync`, `EnvironmentWatchTests.INF_TLS_003_AC2_AnUnwatchedRenewalIsRaisedAsADegradationAsync` |
| INT-GEN-006 | the item, AC1, AC2, AC3 | `LocationDatabaseTests.INT_GEN_006_AC1_EveryAddressIsResolvedAgainstTheCopyHeldAsync`, `LocationDatabaseTests.INT_GEN_006_AC2_AFailedRefreshSurfacesAsADegradationAsync`, `LocationDatabaseTests.INT_GEN_006_AC2_ARefreshReplacesTheCopyHeldAsync`, `LocationDatabaseTests.INT_GEN_006_AC3_TheCityIsWhatTheFileSaysOfTheAddressAsync`, `LocationDatabaseTests.INT_GEN_006_AC3_WithNoFileAvailableNoLocationIsAnsweredAsync`, `LocationDatabaseTests.INT_GEN_006_AFileThatCannotBeReadWholeIsRefusedAsync`, `LocationDatabaseTests.INT_GEN_006_AFileWithNoDateIsRefusedAsync`, `LocationDatabaseTests.INT_GEN_006_AStaleFileAnswersNoLocationAndIsRaisedAsync` |
| INT-MAIL-006 | AC1a | `BootstrapTests.INT_MAIL_006_AC1a_TheAdministratorsMailboxIsQueuedAsync` |
| INT-SMS-004 | AC2 | `SendingServiceTests.INT_SMS_004_AC2_ARetryIsHeldBelowTheFloorAsync` |
| LIB-API-001 | the item | `EventPublisherTests.LIB_API_001_EveryEmittedEventHasItsConsumers`, `PendingEventsTests.LIB_API_001_EveryEmittedEventReadsBackAsItWasRaisedAsync` |
| OPS-ALERT-001 | the item, AC1 | `AlertChannelsTests.OPS_ALERT_001_AC1_ARaisedConditionWaitsForTheChannelsAsync`, `AlertDispatchTests.OPS_ALERT_001_AC1_ARaisedConditionIsCarriedOnceAsync`, `AlertDispatchTests.OPS_ALERT_001_AConditionTheRouterRefusedWaitsAsync`, `ConfigurationAdministrationTests.OPS_ALERT_001_AC1_ASystemPolicyAskingLessAtAGateRaisesTheAlertAsync`, `ConfigurationAdministrationTests.OPS_ALERT_001_AC1_ASystemPolicyAskingMoreAtAGateRaisesNothingAsync`, `GateBehaviourTests.OPS_ALERT_001_AC1_ADenialSpikeOfOneActorIsRaisedAsync`, `GateBehaviourTests.OPS_ALERT_001_AC1_ARunOfRefusalsNamingNoOneIsRaisedAsync`, `HolidayListWatchTests.OPS_ALERT_001_AC1_AHolidayListRunningOutIsRaisedAsync`, `OrganizationPolicyEndpointTests.OPS_ALERT_001_AC1_AnOrganizationPolicyAskingLessAtAGateRaisesTheAlertAsync`, `RaisedAlertsTests.OPS_ALERT_001_AC1_ACarriedConditionIsRemovedAsync`, `RaisedAlertsTests.OPS_ALERT_001_AC1_ARaisedConditionReadsBackAsItWasRaisedAsync`, `RecoveryServiceTests.OPS_ALERT_001_RecoveryClusteringOnOneAccountIsRaisedAsync` |
| OPS-ALERT-005 | the item, AC1, AC2 | `ReadVolumeStoreTests.OPS_ALERT_005_ARecountReplacesEveryMeanAsync`, `ReadVolumeStoreTests.OPS_ALERT_005_ReportsOnOneDayAddToOneCountAsync`, `ReadVolumeStoreTests.OPS_ALERT_005_TheMeanIsRecomputedOverTheWindowBeforeTodayAsync`, `ReadVolumeTests.OPS_ALERT_005_AC1_AnActorReadingFarBeyondTheirOwnPatternRaisesAsync`, `ReadVolumeTests.OPS_ALERT_005_AC2_AnActorWhoseNormalIsHighDoesNotAlertAsync`, `ReadVolumeTests.OPS_ALERT_005_ADayIsTheCalendarDayInTheZoneAsync`, `ReadVolumeTests.OPS_ALERT_005_ANegativeCountIsMalformedAsync`, `ReadVolumeTests.OPS_ALERT_005_ASystemPrincipalIsNotCountedAsync`, `ReadVolumeTests.OPS_ALERT_005_AlertingOffCountsAndRaisesNothingAsync`, `ReadVolumeTests.OPS_ALERT_005_TheActorIsCountedNotThePersonActedForAsync`, `ReadVolumeTests.OPS_ALERT_005_TheFactorAndMinimumAreTheConfiguredOnesAsync`, `ReadVolumeTests.OPS_ALERT_005_TheMeanIsTakenOverTheWindowBeforeTodayAsync`, `ReadVolumeTests.OPS_ALERT_005_TheMinimumKeepsAFirstBusyDaySilentAsync`, `ReadVolumeTests.OPS_ALERT_005_TheWindowIsTheConfiguredOneAsync` |
| OPS-ALERT-006 | the item, AC1, AC2 | `AuthorizationModelTests.OPS_ALERT_006_AC1_ExportIsAnEnumeratedSetOfOperations`, `ConfigurationEndpointTests.OPS_ALERT_006_AC2_ExportAuditingCannotBeDisabledThroughTheApplicationAsync`, `ExportOperationsTests.OPS_ALERT_006_AC1_OnlyADeclaredExportIsGatedLimitedAndRecordedAsync`, `ExportOperationsTests.OPS_ALERT_006_AC2_OnlyTheDeploymentTurnsTheAuditOffAsync`, `ExportOperationsTests.OPS_ALERT_006_ALimitOfNothingAdmitsNoExportAsync`, `ExportOperationsTests.OPS_ALERT_006_AnExportAsksForStepUpWhileTheDeploymentRequiresItAsync`, `ExportOperationsTests.OPS_ALERT_006_AnExportPastTheHourlyLimitIsThrottledUntilAPlaceFreesAsync`, `ExportOperationsTests.OPS_ALERT_006_EachAdmittedExportIsIndividuallyAuditedAsync`, `ExportOperationsTests.OPS_ALERT_006_TheLimitIsEachActorsOwnAsync`, `ExportStoreTests.OPS_ALERT_006_AnAdmittedExportIsRecordedOnItsOwnAsync`, `ExportStoreTests.OPS_ALERT_006_EachActorsHourHoldsItsOwnExportsAsync`, `GateBehaviourTests.OPS_ALERT_006_AnExportIsGatedRecordedAndLimitedAtTheGateAsync` |
| OPS-ALERT-007 | the item, AC1, AC2 | `SessionServiceTests.OPS_ALERT_007_AC1_CitiesFurtherApartThanTheDistanceAlertAsync`, `SessionServiceTests.OPS_ALERT_007_AC1_SimultaneousSessionsFromImplausibleOriginsAlertAsync`, `SessionServiceTests.OPS_ALERT_007_AC2_OrdinaryMultiDeviceUseDoesNotAsync`, `SessionServiceTests.OPS_ALERT_007_ASessionTakenUpAgainFarAwayAlertsAsync`, `SessionServiceTests.OPS_ALERT_007_TheDistanceIsTheConfiguredOneAsync`, `SessionStoreTests.OPS_ALERT_007_WhereACityLiesReadsBackAsWrittenAsync` |
| OPS-BOOT-001 | the item, AC1, AC2, AC3, AC4 | `BootstrapRefusalTests.OPS_BOOT_001_AC4_BootstrapWithoutTheGoverningLanguageIsRefusedByNameAsync`, `BootstrapRefusalTests.OPS_BOOT_001_AKeyThatDoesNotNameTheDeploymentIsRefusedAsync`, `BootstrapRefusalTests.OPS_BOOT_001_AValueItsKeyDoesNotAdmitIsRefusedAsync`, `BootstrapRefusalTests.OPS_BOOT_001_AValueTheDeploymentLeftUnnamedIsRefusedByItsKeyAsync`, `BootstrapTests.OPS_BOOT_001_AC1_RunningItAgainWhileASystemAdministratorExistsIsRefusedAsync`, `BootstrapTests.OPS_BOOT_001_AC2_NoAccountHoldsACredentialAsync`, `BootstrapTests.OPS_BOOT_001_AC3_NoEmergencyCredentialIsIssuedAndItsAbsenceIsRaisedAsync`, `BootstrapTests.OPS_BOOT_001_AFreshDeploymentIsStoodUpByTheCommandAsync`, `BootstrapTests.OPS_BOOT_001_TheAdministrativeRolesAreSeededAsync`, `BootstrapTests.OPS_BOOT_001_TheNamedValuesAndTheAdministrativePolicyAreWrittenAsync`, `BootstrapTests.OPS_BOOT_001_ThePrintedAddressEnrolsTheFirstAdministratorAsync`, `EmergencyCredentialWatchTests.OPS_BOOT_001_AC3_ASpentCredentialLeavesTheAbsenceRaisedAsync`, `EmergencyCredentialWatchTests.OPS_BOOT_001_AC3_TheAbsenceIsRaisedUntilACredentialIsGeneratedAsync` |
| OPS-BOOT-002 | the item, AC1, AC2, AC3, AC4, AC5, AC6 | `AccountAdministrationTests.OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedAsync`, `AccountStatesTests.OPS_BOOT_002_TheReservedAccountIsNeverTakenDownAsync`, `AccountTests.OPS_BOOT_002_TheEmergencyAccountIsNeverSuspendedOrDeleted`, `BootstrapRefusalTests.OPS_BOOT_002_AC5_BootstrapWithoutTheOwnersDestinationsIsRefusedAsync`, `BootstrapTests.OPS_BOOT_002_TheEmergencyAccountHoldsTheRoleAndNoWayInAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC1_UseConsumesTheCredentialAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC2_TheSessionExpiresAtTheConfiguredLifetimeAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC3_UseReachesTheOwnerWithOwnerAlertsOffAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC4_TheSessionApprovesARecoveryAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC4_TheSessionGrantsSystemAdministrationAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_AC6_AnApplicationOpensFromTheSessionAsync`, `BreakGlassEndpointTests.OPS_BOOT_002_NoSignInMethodIsGivenToTheReservedAccountAsync`, `BreakGlassStoreTests.OPS_BOOT_002_AC1_AnIssueIsSpentOnceAsync`, `GrantEndpointTests.OPS_BOOT_002_TheReservedAccountIsGrantedNothingAsync`, `GroupEndpointTests.OPS_BOOT_002_TheReservedAccountJoinsNoGroupAsync` |
| OPS-BOOT-004 | the item, AC1, AC2, AC3, AC4, AC6, AC7 | `BreakGlassCodeTests.OPS_BOOT_004_AC6_ACodeTypedAsPrintedIsRead`, `BreakGlassCodeTests.OPS_BOOT_004_AC6_AGroupWithAWrongCheckIsRefused`, `BreakGlassCodeTests.OPS_BOOT_004_AC6_ASymbolMistypedInTheFirstOrThirdPlaceIsCaught`, `BreakGlassCodeTests.OPS_BOOT_004_AC6_TheCheckIsTheWeightedSumOfTheGroup`, `BreakGlassCodeTests.OPS_BOOT_004_AC6_TheCodeCarriesAtLeast128BitsFromATypeableAlphabet`, `BreakGlassEndpointTests.OPS_BOOT_004_AC1_ABreakGlassSessionGeneratesAReplacementAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC2_GenerationIsAuditedAndReachesTheOwnerAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC3_ASecondGenerationInvalidatesTheFirstAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC6_AWrongCheckIsRefusedBeforeAnyHashIsComparedAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_AC7_TheSixthAttemptInAnHourIsRefusedFromAnySourceAsync`, `BreakGlassEndpointTests.OPS_BOOT_004_GenerationAsksForASteppedUpSystemAdministratorAsync`, `BreakGlassStoreTests.OPS_BOOT_004_AC3_AReplacementStandsInThePreviousPlaceAsync`, `BreakGlassStoreTests.OPS_BOOT_004_AC4_OnlyAHashOfTheCodeIsHeldAsync`, `BreakGlassStoreTests.OPS_BOOT_004_AC7_EveryAttemptIsCountedWithinTheWindowAsync`, `BreakGlassStoreTests.OPS_BOOT_004_AnIssueEndsOnceAsync`, `BreakGlassStoreTests.OPS_BOOT_004_OneIssueStandsAtATimeAsync` |
| OPS-CFG-002 | AC1 | `ConfigurationAdministrationTests.OPS_CFG_002_AC1_TheSystemPolicyIsClassifiedFieldByFieldAsync` |
| OPS-CFG-004 | the item, AC2 | `ConfigureTests.OPS_CFG_004_AC2_AProtectedKeyIsChangedFromTheServerWrittenDownAndRaisedAsync`, `ConfigureTests.OPS_CFG_004_AChangeThatLeavesTheDeploymentUnableToStartIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AChangeWithoutAReasonIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AKeyTheApplicationChangesIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AValueItsKeyDoesNotAdmitIsRefusedAsync`, `ConfigureTests.OPS_CFG_004_AnOrganizationsStepUpEnforcementIsSwitchedFromTheServerAsync`, `ConfigureTests.OPS_CFG_004_TheGoverningLanguageIsRaisedUnderItsOwnConditionAsync`, `LibraryStructureTests.OPS_CFG_004_AC2_OnlyTheCommandLineWritesAProtectedKey` |
| OPS-ENV-001 | AC1, AC2 | `BootstrapTests.OPS_ENV_001_AC1_AFreshDatabaseYieldsAUsablePermissionRealisticDatasetAsync`, `FailClosedTests.OPS_ENV_001_AC2_NoBypassFlagExistsInAnyEnvironment` |
| OPS-ENV-002 | AC1 | `BrowserProfileTests.OPS_ENV_002_AC1_TheProfileAnswersAConcealedRefusalAndKeepsWhatItsStagesWroteAsync` |
| OPS-MAINT-001 | the item, AC1, AC2, AC3 | `DatabaseRoleTests.OPS_MAINT_001_AC3_TheApplicationCannotChangeOrRemoveALogEntryAsync`, `LicenceExpiryTests.OPS_MAINT_001_AC2_ALicenceInsideTheLeadRaisesExpiryApproachingAsync`, `LicenceExpiryTests.OPS_MAINT_001_AC2_ALicenceThatLapsedUnrenewedIsStillRaisedAsync`, `LicenceExpiryTests.OPS_MAINT_001_AC2_TheLeadIsTheConfiguredOneAsync`, `MaintenanceEndpointTests.OPS_MAINT_001_ABodyThatCannotBeReadIsMalformedAsync`, `MaintenanceEndpointTests.OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync`, `MaintenanceEndpointTests.OPS_MAINT_001_AC3_ARecordedTaskIsDatedAndCarriesThePersonAskingAsync`, `MaintenanceEndpointTests.OPS_MAINT_001_AnAccountWithoutComplianceManageIsRefusedAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_AC3_ATaskDatedAfterNowIsRefusedAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_AC3_AnEntryIsDatedAndCarriesThePersonAskingAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_EveryOperationAnswersToComplianceManageAsync`, `MaintenanceRecordsTests.OPS_MAINT_001_TwoLicencesUnderOneIdentifierAreRefusedAsync`, `MaintenanceStoreTests.OPS_MAINT_001_AC1_TheExpiryDatesReadBackAsTheyWereReplacedAsync`, `MaintenanceStoreTests.OPS_MAINT_001_AC3_AnEntryReadsBackDatedAndWithItsActorAsync` |
| OPS-MIG-003 | the item | `DatabaseRoleTests.OPS_MIG_003_TheApplicationReachesTheRowsOfEveryTableAsync` |
| OPS-MIG-003a | the item, AC4 | `AuditPartitionsTests.OPS_MIG_003a_OnlyTheMaintenanceCredentialIsTakenForItAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheFingerprintsAndNoOtherColumnAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheWrappedValuesAndNoOtherColumnAsync`, `KeyMaterialTests.OPS_MIG_003a_StartupFailsNamedWithoutTheMaintenanceCredential` |
| OPS-OBS-001 | AC1, AC2 | `ExplanationTests.OPS_OBS_001_AC1_ADenialResolvesToThePermissionAndTheMissingGrantAsync`, `ExplanationTests.OPS_OBS_001_AC2_OnesOwnRefusalIsExplainedWithoutAnElevatedRoleAsync` |
| OPS-OBS-002 | AC1, AC2 | `CredentialFlowTests.OPS_OBS_002_AC2_AFallbackOutlivesTheOutageUntilItIsCarriedAsync`, `PasswordScreeningTests.OPS_OBS_002_AC1_ABlocklistFallbackRaisesADegradationAsync`, `PasswordScreeningTests.OPS_OBS_002_AC2_AFallbackThatCannotBeRaisedRefusesTheOperationAsync` |
| OPS-OBS-003 | AC1 | `BackgroundJobsTests.OPS_OBS_003_AC1_WhatHasLapsedIsClearedWithNobodyAskingAsync` |
| OPS-SEC-001 | the item, AC2 | `BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesADocumentWithoutTheKeysAsync`, `BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesAFingerprintKeyThatCannotBeUsedAsync`, `BootstrapRefusalTests.OPS_SEC_001_AC2_TheCommandRefusesAKeyThatCannotBeUsedAsync`, `BootstrapRefusalTests.OPS_SEC_001_ARefusalCarriesNoneOfTheDocumentAsync`, `BootstrapRefusalTests.OPS_SEC_001_TheCommandRefusesATerminalAsync`, `ClientRegistryTests.OPS_SEC_001_ASecretTheServerWouldNotTakeIsRefusedAsync` |
| OPS-SEC-002 | the item, AC1, AC2 | `ClientRegistryTests.OPS_SEC_002_AC2_AReplacedSecretIsKeptThroughTheOverlapAsync`, `ClientRegistryTests.OPS_SEC_002_AC2_TheOverlapFollowsTheAccessTokenLifetimeAsync`, `ClientRegistryTests.OPS_SEC_002_AChangeThatKeepsTheSecretReplacesNothingAsync`, `OidcFlowTests.OPS_SEC_002_AC2_AReplacedSecretAuthenticatesTheClientThroughTheOverlapAsync`, `OidcStoreTests.OPS_SEC_002_AC2_AReplacedSecretIsKeptUntilTheOverlapEndsAsync`, `RegisterClientTests.OPS_SEC_002_AC2_RegisteringANewSecretKeepsTheReplacedOneThroughTheOverlapAsync`, `SigningKeysTests.OPS_SEC_002_AC1_TheRunningInstanceRotatesWithoutRestartOrAPersonAsync`, `SigningKeysTests.OPS_SEC_002_AC2_WhatThePreviousKeySignedVerifiesThroughTheOverlapAsync` |
| OPS-SEC-003 | the item, AC1, AC2, AC3, AC4, AC5, AC6 | `FingerprintKeyRotationTests.OPS_SEC_003_AC6_ARotationNeedsANewVersionAndEveryVersionInUseAsync`, `FingerprintKeyRotationTests.OPS_SEC_003_AC6_ASealBeforeTheRotationCompletesIsRefusedAsync`, `FingerprintKeyRotationTests.OPS_SEC_003_AC6_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync`, `FingerprintKeyRotationTests.OPS_SEC_003_AC6_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync`, `FingerprintKeyRotationTests.OPS_SEC_003_AC6_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_AFingerprintItsValueDoesNotComputeStopsTheRunAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_AKilledRunResumesFromItsProgressAndComputesEachFingerprintOnceAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_ALookupMatchesUnderEveryVersionHeldAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_EveryFingerprintIsComputedAgainUnderTheNewVersionAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForAHeldUsernameAndForgetsWhatTheVersionHashedAsync`, `FingerprintRotationTests.OPS_SEC_003_AC6_RetirementWaitsForTheReservationOfAnErasedSubjectAsync`, `KeyRotationTests.OPS_SEC_003_AC1_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync`, `KeyRotationTests.OPS_SEC_003_AC2_AKilledRunResumesFromItsProgressAndReWrapsEachKeyOnceAsync`, `KeyRotationTests.OPS_SEC_003_AC3_AfterRetirementNoValueIsWrappedUnderThePreviousVersionAsync`, `KeyRotationTests.OPS_SEC_003_AC3_RetirementWaitsWhileValuesAreStillWrappedUnderThePreviousVersionAsync`, `KeyRotationTests.OPS_SEC_003_AC4_ASealBeforeTheRotationCompletesIsRefusedAsync`, `KeyRotationTests.OPS_SEC_003_AC4_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync`, `KeyRotationTests.OPS_SEC_003_AC5_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync`, `KeyRotationTests.OPS_SEC_003_ARotationNeedsANewVersionAndEveryVersionInUseAsync`, `LibraryStructureTests.OPS_SEC_003_AC1_OnlyTheCommandLineRunsTheRotation`, `PersonalFieldCipherTests.OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError` |
| PRIV-RET-002 | AC3, AC5 | `AuditPartitionsTests.PRIV_RET_002_AC3_TheMonthsAheadAreCreatedAndTheExpiredDroppedAsync`, `AuditRetentionTests.PRIV_RET_002_AC3_ExpiredPartitionsAreDroppedOnScheduleWithoutAPersonAsync`, `AuditRetentionTests.PRIV_RET_002_AC5_ARunUnderAnotherCredentialDropsNothingAsync` |
| PRIV-RIGHT-002 | the item | `HolidayListWatchTests.PRIV_RIGHT_002_AListReachingPastTheLeadRaisesNothingAsync`, `HolidayListWatchTests.PRIV_RIGHT_002_AnEmptyHolidayListIsRaisedAsync`, `HolidayListWatchTests.PRIV_RIGHT_002_TheLeadIsTheConfiguredOneAsync` |
| PRIV-RIGHT-004 | the item | `SubjectEraserTests.PRIV_RIGHT_004_AnErasedAccountHoldsNoRestrictionAsync` |
| PRIV-ROPA-001 | the item | `ProcessingRecordsTests.PRIV_ROPA_001_WhatAPersonSuppliesIsCommittedAsync` |

Criteria no test decides, and how each was verified (CONV-TEST-007):

| Criterion | Verified by |
|---|---|
| OPS-BOOT-003 AC1 | Inspection: chapter 11 records that break-glass custody sits with the owner |
| OPS-SEC-001 AC1, the repository half | The pipeline's secret scanning job over the whole history; the image half has no image in Milestone 1 (section 2) |
| DR-009a AC1, "rotation occurs at its expiry" | The rotation is the human step of chapter 06 section 9. What the library holds of it is the warning ahead of the cryptoperiod's end (`EnvelopeRotationWatchTests`) and the command that rotates (`KeyRotationTests`), entry 341 |
| BFF-ERR-003 AC2, AUTHZ-CONCEAL-002 AC2 | Construction: one refusal path and one writer answer every concealed refusal; byte identity is asserted by BFF-ERR-003 AC1 (entry 339) |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| OPS-BOOT-004 AC5 | The `/break-glass` page is the frontend's (FE-BG-001); the library reads the code typed as printed, grouped or not (AC6) | Milestone 2 step 10 |
| OPS-SEC-001 AC1, the image half | No image is built in Milestone 1 | Milestone 2 step 3 |
| OPS-SEC-001 AC3 | What the host holds outside the secrets manager is the host's | Milestone 2 step 4 |
| OPS-SEC-002 AC1, client secrets | The sign-on secret is read once at startup (D-162 item 66, `ISecretSource`), so a client secret is rotated in three human steps inside the overlap; a contradiction recorded in entry 340. AC1 is met for the signing keys, AC2 for both | The owner's review of entry 340 |
| DR-016 AC1 | Storage that does not share fate with the database host is the deployment's; the library writes through `IErasureLedger` | Milestone 2 step 9 |
| DR-007 AC5, DR-017 AC2 | The throwaway instance built from the committed infrastructure definition; the library restores into whatever `IRestoreTestInstance` the host registers | Milestone 2 steps 3 and 9 |
| DR-010 AC2 | The backup key fetched at run time by the restore | Milestone 2 step 9 |
| INF-HOST-001 AC1, INF-TLS-003 AC1 and AC3 | The clock kept within tolerance and the served certificate's expiry are the environment's; the library raises what `IClockReference` and `ICertificateRenewal` report (entry 330) | Milestone 2 steps 3 and 4 |
| A route showing `no-emergency-credential` to every system administrator | Chapter 09 names none; the alert is raised through the channels (entry 331) | The owner's review of entry 331 |
| Chapter 06 items outside this phase's list whose criteria carry no test name: OPS-DATA-001 AC2, OPS-DATA-002 AC3, OPS-DATA-003, OPS-DB-002 AC2, OPS-DB-003, OPS-MIG-001 AC2, OPS-MIG-002 AC2, OPS-MIG-003 AC2, OPS-MIG-004, OPS-MIG-005, OPS-MIG-006, OPS-MIG-007 AC2, OPS-DEP-001 to OPS-DEP-005, OPS-CFG-001, OPS-CFG-006, OPS-CFG-008 AC2 | Found by the coverage sweep of chapters 06 and 12; the pipeline and deployment criteria among them are Milestone 2's, and the rest are checked in the exit-gate pass | Phase 10, the exit-gate coverage pass |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `LibraryStructureTests.CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType` | The pattern held a raw backspace byte where the word boundary `\b` was meant, so its `string subject\|organization\|email\|phone\|username\|address` branch never matched and only bare `Guid` parameters were checked | CONV-DESIGN-004 AC2, "No method takes a bare `Guid` or `string` where a typed identifier or value exists", the working guide's section 3, a gate that mis-implements its own rule | The pattern matches the word boundary. It then found: the mailbox address carried as text where `EmailAddress` exists, now typed through `Mailbox`, `IMailboxStore` and their callers; a provider's subject identifier, an IP address and an endpoint named as if they were the library's values, now named `providerSubject`, `ipAddress` and `endpoint`; and OpenIddict's store members, whose signature is OpenIddict's, which the scan no longer reads |
| `BackgroundWorkerTests.INF_BG_002_EveryScheduledJobIsANamedRestrictedPrincipal` | The test read an item as needing two prefixes before its number, which chapter 12's `DR-NNN` items do not have | INF-BG-002 and CONV-TEST-007, the working guide's section 3, test infrastructure | The pattern takes one prefix or more |
| `DatabaseFixture`, `DatabaseRoleTests`, `AuditPartitionsTests`, `AuditStoreTests` | The integration cases stamp audit rows in September 2026 while the migration creates the partitions of the day it runs, so the suite would have failed from December 2026 for want of a partition, and three cases counted partitions the calendar changes | PRIV-RET-002 and CONV-TEST-007, the working guide's section 3, test infrastructure | `DatabaseFixture` creates the September to November 2026 months under the migration function's names; the two drop cases use a routine retention of 30 years against a 1990 partition; `AuditStoreTests.PRIV_RET_002_AC3_TheSweepKeepsThreeMonthsOpenPerCategoryAsync` counts the three months from today by name. No runtime code changed |
| `ContainerRestore` | The restore of a real backup lived in the hosting test project, out of reach of the command-line tests of DR-006a AC1 | DR-006a AC1 and CONV-TEST-007, the working guide's section 3, test infrastructure | Moved to `Janus.Storage.Tests` and made public, so both projects restore with the one fake the restore test uses. No runtime code changed |
| `docs/reports/decisions-pending-review.md`, three places | The ledger spelled the code `model.startup.keyunavailable` | `10` section 1, `ErrorCodes.StartupKeyUnavailable` | The ledger spells it `model.startup.kekunavailable` |
| `docs/reports/decisions-pending-review.md`, the audit actions rows for chapter 10 | The four `ops.keyrotation` actions entries 316 and 318 write were missing from the table | Entries 316 and 318 | The rows list them, with `ops.restoretest.completed` |

## 4. Decided in the owner's absence

Under D-161 as amended by D-162. Each entry is in `docs/reports/decisions-pending-review.md`
in the same words, numbered as it is there.

| # | Tier | Decision |
|---|---|---|
| 290 | Tier 3 | How a raised condition reaches the alert channels |
| 291 | Tier 3 | Generating the break-glass credential raises the break-glass alert |
| 292 | Tier 3 | Every attempt at the break-glass credential counts against the global limit |
| 293 | Tier 3 | What a refused break-glass code is answered |
| 294 | Tier 2 | How the reserved `emergency` account is marked and found |
| 295 | Tier 3 | What the reserved account is refused, and with which code |
| 296 | Tier 3 | How long the break-glass session lives |
| 297 | Tier 2 | Break-glass has no service contract in `Janus.Core` |
| 298 | Tier 2 | The address the generated page carries |
| 299 | Tier 2 | One break-glass issue stands at a time |
| 300 | Tier 3 | How the break-glass code is hashed |
| 301 | Tier 3 | What "any cookie present is ignored" does at `/auth/break-glass` |
| 302 | Tier 3 | The owner's stated reason has nowhere to be stated |
| 303 | Tier 3 | How an action of background work is audited, and what refuses it to nobody |
| 304 | Tier 3 | Which pool-wide operations the scheduled jobs run as |
| 305 | Tier 2 | How often the jobs run that no setting paces |
| 306 | Tier 3 | How far back the token sweep reaches, and the one sweep not scheduled |
| 307 | Tier 3 | How the command-line application reaches the deployment's keys |
| 308 | Tier 2 | What `janus bootstrap` takes on its command line, and how it refuses |
| 309 | Tier 2 | How bootstrap queues the first administrator's mailbox |
| 310 | Tier 2 | The enrolment address bootstrap prints |
| 311 | Tier 3 | What "a system administrator exists" means to bootstrap |
| 312 | Tier 2 | The canary subject and the reserved account bootstrap seeds |
| 313 | Tier 3 | Who grants what bootstrap grants, and how its alert is raised |
| 314 | Tier 3 | What bootstrap records in the audit trail |
| 315 | Tier 3 | How bootstrap writes the values it sets |
| 316 | Tier 3 | What the key-encryption key's rotation re-wraps, and what the maintenance credential reaches for it |
| 317 | Tier 3 | How a key-encryption key rotation starts, is confirmed and retires |
| 318 | Tier 3 | What the fingerprint key's rotation computes again, and what it keeps until the previous version retires |
| 319 | Tier 3 | How a protected key is changed from the server, and what the change records and raises |
| 320 | Tier 2 | The library carries its own events: a row on the transaction, and a publisher that offers it to the host's consumers |
| 321 | Tier 2 | What a denial spike counts, and when it is raised |
| 322 | Tier 2 | How a message no transport took is carried again |
| 323 | Tier 2 | The licence and maintenance log endpoints, and who may use them |
| 324 | Tier 2 | When the holiday list has run out, and what looks |
| 325 | Tier 2 | Where the IP-to-city file comes from, what it looks like, and how a process holds it |
| 326 | Tier 2 | When two sessions are looked at together, and what is kept to measure them |
| 327 | Tier 3 | Where read volume is counted from, and how a person's normal is kept |
| 328 | Tier 3 | How a host's action bound to a step-up gate is met |
| 329 | Tier 3 | What an export operation is, and what it asks |
| 330 | Tier 2 | How the library learns of clock drift and a failed certificate renewal |
| 331 | Tier 2 | How the missing emergency credential stays raised, and where it is shown |
| 332 | Tier 3 | How the off-host erasure ledger is written, and what an erasure waits for |
| 333 | Tier 3 | What the replay of the erasure ledger does to a restored database |
| 334 | Tier 3 | How the restore test is run, proved, timed and recorded |
| 335 | Tier 2 | How the recovery-code reminder is sent |
| 336 | Tier 3 | How the audit partitions are kept, and how the maintenance credential reaches the worker |
| 337 | Tier 2 | How a blocklist fallback is raised |
| 338 | Tier 2 | A development database is made ready the way a production one is |
| 339 | Tier 3 | A concealed refusal is answered by the browser profile |
| 340 | Tier 3 | How a client enters the registry, and how its secret is rotated |
| 341 | Tier 3 | How the key-encryption key's cryptoperiod is kept |
| 342 | Tier 3 | A restriction names no channel |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The local counts
at the end of the phase: `Janus.Analyzers.Tests` 17, `Janus.Authentication.Tests` 733,
`Janus.Authorization.Tests` 117, `Janus.Cli.Tests` 13 and 45 integration,
`Janus.Core.Tests` 444, `Janus.Hosting.Tests` 576 and 170 integration,
`Janus.Identity.Tests` 82, `Janus.Privacy.Tests` 218 and `Janus.Storage.Tests` 33 and
377 integration, none failing.

The full gate was run once on the development machine, at `66f0c09`, all green but
one job:

- the integration suites against containers: `Janus.Cli.Tests` 45,
  `Janus.Hosting.Tests` 170 and `Janus.Storage.Tests` 377;
- the contract suite (75), the policy coverage test (3) and the truth-table suite (49);
- the migrations applied from empty to two throwaway PostgreSQL 17 databases created
  as the double-migration job creates them; with no release tagged yet, the second run
  also starts from empty; both applied again with nothing left to apply;
- no model change without its migration;
- the forbidden-marker, acceptance-criterion test name and changelog jobs over
  `main..HEAD`.

The commit message job over `main..HEAD` fails on one commit of this phase:
`fix(storage): append audit records through the operation's connection` carries a body
point wrapped onto a second line that is not a dash fragment. The fix is a reword of
that one unpushed commit, which rewrites the branch after it. That rewrite was not made
on the development machine and waits on the owner's permission. No other commit in the
range fails the job. The commit after the full gate adds one unit test and its ledger
entry, and was run with the fast checks and the two naming jobs.

The pipeline run, which is the gate of record, has not been made: the branch
`phase-09-operations` is not pushed, and pushing waits on the owner's permission.
