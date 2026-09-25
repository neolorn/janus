# Phase 8: Organizations, invitations, domain lock, mail

Status: complete on the development machine, no open question; the pipeline run of
the full gate waits on the branch being pushed (section 5). Invitations bound and
open, with the personal email a staff invitation names verified by the press on its
link; the acknowledgement that attaches the membership, records the documents shown
and gives the corporate address and its mailbox; acceptance by an account that
already exists; the end of a membership, which makes the personal email primary in
the same transaction and leaves the account's state alone; the domain lock with DNS
verification and its scheduled re-check; the mailbox provisioned disabled at
invitation and enabled at acknowledgement, the lifecycle push, reconciliation and the
app passwords made through the mail server's first-party client; the warning of a
sending domain the private relay does not know; the two-phase takedown; and the
administration surface of chapter 09 sections 6a, 8 and 8a, are built. Two items of
earlier phases found unbuilt or unreported are built here: IDN-ATTR-001 AC2 and AC3
(phase 1) and IDN-LIFE-003a AC1 and AC5 (phase 7). The decisions taken in the owner's
absence are in section 4 and, in the same words, in
`docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| API-CONV-002 | the item | `GrantEndpointTests.API_CONV_002_AReadOfHeldGrantsNamesWhoseAndWhereAsync`, `GrantEndpointTests.API_CONV_002_AReasonPastTheLimitIsMalformedAsync` |
| AUTH-ABUSE-004 | AC3, AC4 | `RestrictionEndpointTests.AUTH_ABUSE_004_AC3_ALooseningWithAReasonTakesEffectAndAlertsAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC3_ALooseningWithoutAReasonIsRefusedAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC3_ATighteningTakesEffectAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC3_AnEditWithoutStepUpIsRefusedAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC4_AGrantNeedsItsPermissionAndAReasonAsync`, `RestrictionEndpointTests.AUTH_ABUSE_004_AC4_TheSupportRoleGrantsCreditAsync` |
| AUTH-FACT-017 | the item | `ConfigurationAdministrationTests.AUTH_FACT_017_AChangeToTheSystemPolicyRecordsWhatItRaisedAsync` |
| AUTH-OIDC-001 | AC4 | `AppPasswordsTests.AUTH_OIDC_001_AC4_OnlyThePersonsLiveSessionObtainsATokenAsync` |
| AUTH-SESS-005b | the item | `OrganizationPolicyEndpointTests.AUTH_SESS_005b_TheAdministrativeOrganizationKeepsItsFloorAsync` |
| AUTH-SESS-009 | AC3 | `SessionRevocationEndpointTests.AUTH_SESS_009_AC3_EverySessionIsRevokedOverTheEndpointAsync` |
| AUTH-SESS-010 | AC2 | `AccountStatesTests.AUTH_SESS_010_AC2_TheTakedownEndsSessionsInTheTransactionOfTheSuspensionAsync` |
| AUTH-SESS-011 | AC1 | `SessionRevocationEndpointTests.AUTH_SESS_011_AC1_OneAccountsSessionsAreRevokedOverTheEndpointAsync` |
| AUTH-STEP-001 | the item | `GrantEndpointTests.AUTH_STEP_001_GrantingAndRevokingAreStepUpActionsAsync`, `GroupEndpointTests.AUTH_STEP_001_AChangeOfMembersIsAStepUpActionAsync`, `OrganizationEndpointTests.AUTH_STEP_001_ADeletionAndItsCancellationAreAStepUpActionAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_001_AChangeOfPolicyIsAStepUpActionAsync`, `RoleEndpointTests.AUTH_STEP_001_DefiningAndRemovingARoleAreStepUpActionsAsync` |
| AUTH-STEP-002a | the item | `OrganizationPolicyEndpointTests.AUTH_STEP_002a_AFieldLooserThanTheSystemIsRefusedAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_002a_AFieldTheSystemHasOvertakenIsInheritedAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_002a_ATighteningIsWrittenDownAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_002a_ThePolicyIsGovernedFromTheAdministrativeOrganizationAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_002a_ThePolicyIsReadWithEachFieldMarkedAsync`, `OrganizationPolicyEndpointTests.AUTH_STEP_002a_WhatAReplacementNamesMustBeReadableAsync`, `PolicyResolutionTests.AUTH_STEP_002a_AnOverrideNamingSomeGatesKeepsTheSystemsForTheRestAsync`, `PolicyStrictnessTests.AUTH_STEP_002a_AFieldStatedLooserThanTheSystemIsNamed`, `PolicyStrictnessTests.AUTH_STEP_002a_AStatedFieldIsTheOrganizationsOwnWhileItIsInForce`, `PolicyStrictnessTests.AUTH_STEP_002a_AnOverrideNoLooserThanTheSystemIsAllowed` |
| AUTHZ-CONCEAL-005 | AC1 | `AuditTrailEndpointTests.AUTHZ_CONCEAL_005_AC1_TheReadIsRefusedWithoutThePermissionAsync`, `PublicationEndpointTests.AUTHZ_CONCEAL_005_AC1_PublicationIsRefusedWithoutThePermissionAsync`, `RoleEndpointTests.AUTHZ_CONCEAL_005_AC1_RoleManageIsAskedInTheAdministrativeOrganizationAsync`, `SessionRevocationEndpointTests.AUTHZ_CONCEAL_005_AC1_TheRevocationsAreRefusedWithoutThePermissionAsync` |
| AUTHZ-DERIVE-007 | AC1, AC2 | `ReverseLookupTests.AUTHZ_DERIVE_007_AC1_StoredAndDerivedGrantsAreReportedDistinctlyAsync`, `ReverseLookupTests.AUTHZ_DERIVE_007_AC2_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync` |
| AUTHZ-GATE-004 | AC3, AC4 | `ExplanationEndpointTests.AUTHZ_GATE_004_AC3_AnIdentifierOfNoRefusalOfTheCallersIsRefusedAsync`, `ExplanationEndpointTests.AUTHZ_GATE_004_AC3_TheCallerResolvesTheirOwnRefusalOverTheEndpointAsync`, `ExplanationEndpointTests.AUTHZ_GATE_004_AC4_TheSupportResolutionNeedsTheReadRoleAsync`, `ExplanationTests.AUTHZ_GATE_004_AC3_AnIdentifierResolvesForItsOwnerOnlyWhereTheTypeDisclosesAsync`, `ExplanationTests.AUTHZ_GATE_004_AC3_AnotherPrincipalsIdentifierDoesNotResolveForTheCallerAsync`, `ExplanationTests.AUTHZ_GATE_004_AC4_AReadRoleOutsideTheAdministrativeOrganizationResolvesNothingAsync` |
| AUTHZ-GRANT-001 | AC2 | `GrantEndpointTests.AUTHZ_GRANT_001_AC2_AnOrganizationWideGrantIsWrittenWithNoResourceAsync` |
| AUTHZ-GRANT-002 | the item | `GrantEndpointTests.AUTHZ_GRANT_002_ADenyIsTheSameRowWithItsFlagAsync` |
| AUTHZ-GRANT-003 | AC2, AC3 | `GrantEndpointTests.AUTHZ_GRANT_003_AC2_TheRevocationRecordsWhoWhenAndWhyAsync`, `GrantEndpointTests.AUTHZ_GRANT_003_AC3_AGroupsAndAMaterialisedGrantAreReadWithTheirKindAsync`, `GrantEndpointTests.AUTHZ_GRANT_003_AC3_ReadingWhatAHolderHoldsNeedsGrantReadThereAsync`, `GrantEndpointTests.AUTHZ_GRANT_003_AC3_WhoGrantedWhatAHolderHoldsAndWhenIsReadAsync` |
| AUTHZ-GRANT-004 | AC1 | `RoleEndpointTests.AUTHZ_GRANT_004_AC1_ARoleIsCreatedWithItsPermissionsWithoutARestartAsync` |
| AUTHZ-GROUP-001 | AC1 | `GroupEndpointTests.AUTHZ_GROUP_001_AC1_GroupsNestThroughTheMembersRouteAsync` |
| AUTHZ-MODEL-004 | the item | `RoleEndpointTests.AUTHZ_MODEL_004_ARoleNamesOnlyDeclaredPermissionsAsync` |
| AUTHZ-SCOPE-001 | the item | `AdministrativeScopeTests.AUTHZ_SCOPE_001_APermissionHeldInAnotherOrganizationIsRefusedAsync`, `AdministrativeScopeTests.AUTHZ_SCOPE_001_APermissionHeldInTheAdministrativeOrganizationIsHonouredAsync`, `ConfigurationEndpointTests.AUTHZ_SCOPE_001_AChangeWithoutConfigManageIsRefusedAsync`, `ConfigurationEndpointTests.AUTHZ_SCOPE_001_AReadWithoutConfigReadIsRefusedAsync`, `GrantEndpointTests.AUTHZ_SCOPE_001_ARecordGrantIsScopedToTheRecordsOrganizationAsync`, `GrantEndpointTests.AUTHZ_SCOPE_001_GrantManageElsewhereDoesNotReachTheRecordAsync`, `GroupEndpointTests.AUTHZ_SCOPE_001_GroupManageIsAskedInTheGroupsOrganizationAsync`, `RestrictionEndpointTests.AUTHZ_SCOPE_001_EditingDoesNotReachGrantingAsync`, `RestrictionEndpointTests.AUTHZ_SCOPE_001_TheSetIsNotReadWithoutRestrictionEditAsync` |
| CONV-DESIGN-006 | the item | `RequestJsonTests.CONV_DESIGN_006_ARequestIsReadThroughTheGeneratedContextsOnly`, `RequestJsonTests.CONV_DESIGN_006_EveryRequestBodyIsDeclaredInAContext` |
| IDN-ATTR-001 | AC2, AC3 (carried from phase 1) | `LossReportsTests.IDN_ATTR_001_AC2_ANoticeTheSweepSendsResolvesItsLanguageWithoutARequestAsync`, `SendingServiceTests.IDN_ATTR_001_AC3_NoKnownLanguageGoesOutInEveryDeclaredOneAsync` |
| IDN-ATTR-002 | the item | `AccountAdministrationTests.IDN_ATTR_002_APhotoThePolicyWithholdsIsNotReadAsync` |
| IDN-ATTR-003 | AC3 | `AccountAdministrationEndpointTests.IDN_ATTR_003_AC3_AnAccountsPhotoIsServedToAnAdministratorAsync`, `AccountAdministrationTests.IDN_ATTR_003_AC3_AnAccountsPhotoIsReadThroughTheGateAsync` |
| IDN-LIFE-003 | AC1, AC2, AC3, AC4, AC5, AC6 | `AccountStatesTests.IDN_LIFE_003_AC4_TheTakedownDestroysNothingAndWritesNoErasureAsync`, `AccountStatesTests.IDN_LIFE_003_AC5_TheReversalRestoresActiveAsync`, `DeletionSweepTests.IDN_LIFE_003_AC5_ATakedownIsErasedWhenItsOwnWindowElapsesAsync`, `OutboxStoreTests.IDN_LIFE_003_AC2_TheLatestTakedownIsReadWithItsConfirmationsAsync`, `ProcedureDocumentTests.IDN_LIFE_003_AC1_TheTakedownProcedureExists`, `TakedownEndpointTests.IDN_LIFE_003_AC5_TheReversalIsAnsweredInsideAndAfterItsWindowAsync`, `TakedownServiceTests.IDN_LIFE_003_AC2_TheHostsProgressIsReadableFromTheTriggerAsync`, `TakedownServiceTests.IDN_LIFE_003_AC3_TheTriggerIsAuditedWithItsReasonAsync`, `TakedownServiceTests.IDN_LIFE_003_AC4_NoErasureIsRequestedAtTheTriggerAsync`, `TakedownServiceTests.IDN_LIFE_003_AC4_TheTriggerTakesTheAccountIntoItsWindowInOneTransactionAsync`, `TakedownServiceTests.IDN_LIFE_003_AC5_AReversalAfterTheWindowIsRefusedAsync`, `TakedownServiceTests.IDN_LIFE_003_AC5_AReversalInsideTheWindowRestoresActiveAsync`, `TakedownServiceTests.IDN_LIFE_003_AC6_TheSuspensionIsAnnouncedAndNoDeletionIsAsync` |
| IDN-LIFE-003a | AC1, AC5 (AC1 and AC5 carried from phase 7) | `DeletionSweepTests.IDN_LIFE_003a_AC1_TheTakedownsErasureWritesItsRowAndDeliveryInOneTransactionAsync`, `SubjectEraserTests.IDN_LIFE_003a_AC5_TheStateTheKeyAndTheFingerprintsCommitTogetherAsync`, `TakedownServiceTests.IDN_LIFE_003a_AC1_TheTriggerWritesItsDeliveryAndNoErasureInOneTransactionAsync` |
| IDN-LIFE-003b | AC2 | `ErasureServiceTests.IDN_LIFE_003b_AC2_EveryIncompleteErasureIsListedAsync`, `OutboxStoreTests.IDN_LIFE_003b_AC2_EveryOutstandingErasureIsReadInOneQueryAsync` |
| IDN-LIFE-009a | AC1, AC2, AC3, AC4 | `GrantEndpointTests.IDN_LIFE_009a_AC1_AGrantInTheAdministrativeOrganizationMakesNoMemberAsync`, `InvitationServiceTests.IDN_LIFE_009a_AC2_AnInvitationExpiresItsLifetimeAfterItIsIssuedAsync`, `InvitationServiceTests.IDN_LIFE_009a_AC4_TheMailboxIsEnabledByTheMembershipAndNeverByRegistrationAsync`, `LibraryStructureTests.IDN_LIFE_009a_AC1_OnlyAnAcknowledgedInvitationMakesAMembership`, `RegistrationFlowTests.IDN_LIFE_009a_AC2_AnInvitationTokenThatOpensNothingIsRefusedAsync`, `RegistrationServiceTests.IDN_LIFE_009a_AC2_TheLinkOpensItsInvitationOnceAndInTimeAsync`, `SessionServiceTests.IDN_LIFE_009a_AC3_APasswordHeldBeforeTheMembershipNoLongerAuthenticatesAsync` |
| IDN-LIFE-013 | AC1 | `AccountAdministrationTests.IDN_LIFE_013_AC1_ReactivationRestoresPriorAccessExactlyAsync`, `AccountStoreTests.IDN_LIFE_013_AC1_TheRowCarriesTheRestrictionToRestoreAsync`, `AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresARestrictionInForce` |
| IDN-LIFE-014 | the item | `AccountAdministrationTests.IDN_LIFE_014_ASelfDeletionIsCancelledOnTheSubjectsBehalfAsync` |
| IDN-MEM-001 | AC1, AC2 | `InvitationServiceTests.IDN_MEM_001_AC1_EndingAnotherMembershipLeavesTheCorporateAddressAsync`, `MembershipEndingTests.IDN_MEM_001_AC2_TheMembershipEndsAndItsRecordStaysAsync` |
| IDN-MEM-002 | the item | `InvitationServiceTests.IDN_MEM_002_AnAccountAtItsMembershipLimitIsRefusedAsync`, `MembershipAttachmentTests.IDN_MEM_002_AnAccountThatMayHoldNoMoreIsRefusedAsync` |
| IDN-ORG-001 | the item | `AdministrativeOrganizationTests.IDN_ORG_001_TheMarkedOrganizationIsTheAdministrativeOneAsync`, `AdministrativeScopeTests.IDN_ORG_001_WithoutAnAdministrativeOrganizationEveryOperationIsRefusedAsync` |
| IDN-ORG-002 | the item | `OrganizationDirectoryTests.IDN_ORG_002_AnOrganizationCreatedIsFoundAsync`, `OrganizationEndpointTests.IDN_ORG_002_AnOrganizationIsCreatedAsync`, `OrganizationEndpointTests.IDN_ORG_002_TheAdministrativeOrganizationIsProtectedAsync`, `OrganizationEndpointTests.IDN_ORG_002_TheLifecycleIsGovernedFromTheAdministrativeOrganizationAsync`, `OrganizationEndpointTests.IDN_ORG_002_WhatAChangeNamesMustBeReadableAsync` |
| IDN-ORG-003 | AC1 | `GateBehaviourTests.IDN_ORG_003_AC1_ASuspendedOrganizationConfersNothingAsync`, `GateBehaviourTests.IDN_ORG_003_AC1_ASuspendedOrganizationsDerivationsConferNothingAsync`, `OrganizationDirectoryTests.IDN_ORG_003_AC1_OnlyCurrentMembersAreNamedAsync`, `OrganizationEndpointTests.IDN_ORG_003_AC1_ADeletionRequestEndsEveryMemberSessionAsync` |
| IDN-ORG-004 | the item | `OrganizationDirectoryTests.IDN_ORG_004_ARequestIsKeptUntilItIsCancelledAsync`, `OrganizationEndpointTests.IDN_ORG_004_ARequestIsCancelledOnlyWithinTheWindowAsync` |
| IDN-ORG-006 | AC1, AC2, AC3 | `OrganizationDomainEndpointTests.IDN_ORG_006_AC1_WithTheLockOffNoAddressIsRefusedAsync`, `OrganizationDomainEndpointTests.IDN_ORG_006_AC2_AnAddressOutsideTheVerifiedListIsRefusedAsync`, `OrganizationDomainEndpointTests.IDN_ORG_006_AC3_TheLockIsAPolicyValueChangedWithoutADeployAsync` |
| INT-MAIL-001 | AC1, AC2 | `IntegrationBoundaryTests.INT_MAIL_001_AC1_NoCodeOpensADatabaseButTheLibrarysOwn`, `ModelTests.INT_MAIL_001_AC2_NoCredentialTypeIsAMailAppPassword` |
| INT-MAIL-002 | AC2 | `ModelTests.INT_MAIL_002_AC2_NoQueryOfTheLibraryCanJoinAnotherStore` |
| INT-MAIL-003 | AC1 | `IntegrationBoundaryTests.INT_MAIL_003_AC1_NoStatementNamesARelationOutsideTheLibrarysSchema` |
| INT-MAIL-004 | AC2 | `OidcFlowTests.INT_MAIL_004_AC2_TheDocumentAdvertisesNoIntrospectionAsync` |
| INT-MAIL-005 | AC2 | `ProcedureDocumentTests.INT_MAIL_005_AC2_TheLimitationIsRecordedAsRA02` |
| INT-MAIL-006 | AC1, AC1b (the reconciliation half), AC1c, AC2, AC4 | `InvitationServiceTests.INT_MAIL_006_AC1_TheMailboxIsOnTheServerBeforeTheMembershipBeginsAsync`, `MailboxPublisherTests.INT_MAIL_006_AC1c_AReservedMailboxIsCreatedDisabledAsync`, `MailboxReconciliationTests.INT_MAIL_006_AC1b_OnlyWhatEitherSideHoldsIsComparedAsync`, `MailboxReconciliationTests.INT_MAIL_006_AC1c_AReservedMailboxIsNoDriftAsync`, `RegistrationFlowTests.INT_MAIL_006_AC2_RegisteringACustomerProvisionsNoMailboxAsync`, `RegistrationFlowTests.INT_MAIL_006_AC4_AnUnreachableMailServerDoesNotBlockRegistrationAsync` |
| INT-MAIL-006a | AC1, AC3 | `MailboxPublisherTests.INT_MAIL_006a_AC1_ASuspendedHolderIsDisabledOnTheFirstPassAsync`, `MailboxReconciliationTests.INT_MAIL_006a_AC3_AnEnabledMailboxOwedDisabledIsDriftAsync` |
| INT-MAIL-007 | AC1, AC2, AC3 | `MailboxPublisherTests.INT_MAIL_007_AC1_APushIsWrittenDownBeforeItLeavesAsync`, `MailboxPublisherTests.INT_MAIL_007_AC1_APushMadeAgainProducesNothingTwiceAsync`, `MailboxPublisherTests.INT_MAIL_007_AC1_AReturnWhileAPushIsOutstandingIsPushedAgainAsync`, `MailboxPublisherTests.INT_MAIL_007_AC3_APushThatSpendsItsBudgetIsVisibleAsync`, `MailboxReconciliationTests.INT_MAIL_007_AC2_ADriftIsReportedAndNothingIsChangedAsync`, `MailboxReconciliationTests.INT_MAIL_007_AC3_AComparisonThatCouldNotBeMadeIsVisibleAsync` |
| INT-MAIL-008 | AC1, AC2 | `IntegrationBoundaryTests.INT_MAIL_008_AC1_NoMailServerIsNamedInTheLibrary`, `MailboxPublisherTests.INT_MAIL_008_AC2_AnotherMailServerIsARegistrationAndNoCodeChangeAsync` |
| INT-MAIL-009 | AC1, AC2 | `IntegrationBoundaryTests.INT_MAIL_009_AC2_NoSourceReachesBothMailboxHostingAndDelivery`, `MailboxPublisherTests.INT_MAIL_009_AC1_MailboxHostingIsARegistrationOfItsOwnAsync` |
| INT-MAIL-010 | AC1, AC2, AC3, AC4 | `AppPasswordFlowTests.INT_MAIL_010_AC1_TheServerIsCalledWithThePersonsTokenAsync`, `AppPasswordFlowTests.INT_MAIL_010_AC4_NoSecretReachesALogAsync`, `AppPasswordsTests.INT_MAIL_010_AC2_TheListingIsWhatTheServerHoldsNowAsync`, `AppPasswordsTests.INT_MAIL_010_AC3_TheRevokedAppPasswordIsGoneAtTheServerAsync` |
| INT-MAIL-011 | AC1, AC2, AC3 | `ConfigurationAdministrationTests.INT_MAIL_011_AC2_ChangingTheSendingDomainToAnUndeclaredOneWarnsAsync`, `ConfigurationAdministrationTests.INT_MAIL_011_AC2_EveryChangeThatLeavesTheDomainUndeclaredWarnsAsync`, `ConfigurationAdministrationTests.INT_MAIL_011_AC3_DeclaringTheDomainIsAConfigurationChangeAsync`, `RelayRegistrationTests.INT_MAIL_011_AC1_AnUndeclaredSendingDomainIsNamedInTheWarningAsync`, `StartupValidationTests.INT_MAIL_011_AC1_AnUndeclaredSendingDomainWarnsAsTheDeploymentStartsAsync` |
| LIB-API-005 | the item | `ConfigurationEndpointTests.LIB_API_005_ANameOutsideTheCatalogueIsMalformedAsync` |
| LIB-HOST-001 | the item | `RestrictionEndpointTests.LIB_HOST_001_AHostKeyWithNoSupplierIsRefusedAsync` |
| OPS-ALERT-004a | AC1 | `ConfigurationEndpointTests.OPS_ALERT_004a_AC1_ADestinationChangeTellsThePreviousDestinationsAsync` |
| OPS-CFG-002 | AC1, AC2 | `ConfigurationEndpointTests.OPS_CFG_002_AC1_ATighteningTakesEffectWithoutStepUpAsync`, `ConfigurationEndpointTests.OPS_CFG_002_AC2_ALooseningRequiresStepUpAsync`, `ConfigurationEndpointTests.OPS_CFG_002_AC2_ALooseningWithStepUpAndAReasonTakesEffectAsync` |
| OPS-CFG-003 | AC1, AC2 | `ConfigurationEndpointTests.OPS_CFG_003_AC1_APasswordFloorBelowTheMinimumIsRejectedAsync`, `ConfigurationEndpointTests.OPS_CFG_003_AC2_ASessionTimeoutAboveTheMaximumIsRejectedAsync` |
| OPS-CFG-004 | AC1 | `ConfigurationEndpointTests.OPS_CFG_004_AC1_AProtectedKeyIsRefusedAsync` |
| OPS-CFG-005 | the item | `ConfigurationAdministrationTests.OPS_CFG_005_AChangedMemberOfAFamilyIsWrittenDownAsync`, `ConfigurationEndpointTests.OPS_CFG_005_EveryChangeCarriesAReasonAsync` |
| OPS-CFG-007 | AC1 | `GrantEndpointTests.OPS_CFG_007_AC1_AGrantManagerWithoutSystemAdministerCannotConferItAsync`, `GroupEndpointTests.OPS_CFG_007_AC1_ChangingAnAdministeringGroupNeedsSystemAdministrationAsync`, `RoleEndpointTests.OPS_CFG_007_AC1_ARoleCarryingSystemAdministrationNeedsItAsync` |
| OPS-CFG-008 | AC1 | `ConfigurationStoreTests.OPS_CFG_008_AC1_AWrittenMemberOfAFamilyIsInForceForTheNextReadAsync` |
| PRIV-BREACH-002 | AC1, AC2 | `AuditStoreTests.PRIV_BREACH_002_AC1_TheTrailNamingASubjectEitherWayTakesTheIndexesAsync`, `AuditStoreTests.PRIV_BREACH_002_AC2_TheTrailReadsTheSameBeforeAndAfterErasureAsync` |
| PRIV-CONS-005 | the item | `PublicationEndpointTests.PRIV_CONS_005_TheNoticePublishesOverTheEndpointAsync` |
| PRIV-CONS-006 | AC2, AC3 | `PublicationEndpointTests.PRIV_CONS_006_AC2_AVersionWithoutGoverningTextIsRefusedOverTheEndpointAsync`, `PublicationEndpointTests.PRIV_CONS_006_AC3_ATranslationAttachesWithoutANewVersionAsync` |
| PRIV-CONS-007 | the item | `PublicationEndpointTests.PRIV_CONS_007_APublicationThatDoesNotSayWhetherItIsMaterialIsMalformedAsync` |
| PRIV-RIGHT-004 | AC2 | `AccountAdministrationEndpointTests.PRIV_RIGHT_004_AC2_ARestrictionIsLiftedAsync`, `AccountAdministrationTests.PRIV_RIGHT_004_AC2_LiftingARestrictionRestoresTheAccountAsync`, `AccountDirectoryTests.PRIV_RIGHT_004_AC2_TheLiftTellsTheSubscribersAsync`, `AccountTests.PRIV_RIGHT_004_AC2_ARestrictionIsHeldThroughADeletionWindow` |
| PRIV-RIGHT-005a | the item | `SubjectEraserTests.PRIV_RIGHT_005a_WhatAnAttachedInvitationBindsGoesWithTheSubjectAsync` |
| PRIV-RIGHT-005c | the item | `SubjectEraserTests.PRIV_RIGHT_005c_TheAddressOfAMailboxGoesWithItsHolderAsync` |
| REG-DOM-001 | AC1, AC2, AC3, AC4 | `DomainStoreTests.REG_DOM_001_AC3_OnlyAVerifiedListedDomainIsDueAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AC1_AListedDomainAdmitsNothingUntilVerifiedAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AC3_AFailedReverificationAlertsAndRevokesNothingAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AC4_ALinkNoLongerSignsInToARemovedDomainAsync`, `OrganizationDomainEndpointTests.REG_DOM_001_AC4_ARemovedDomainRefusesSignInAndAlertsAsync`, `RegistrationServiceTests.REG_DOM_001_AC2_AnOpenEmailOutsideTheListIsRefusedAsync` |
| REG-INV-001 | AC1, AC2, AC3, AC4 | `ExportSourceTests.REG_INV_001_AC3_TheExportCarriesWhatWasAcknowledgedAsync`, `InvitationEndpointTests.REG_INV_001_AC4_AnIntegratedInvitationWithoutAPersonalEmailIsRefusedAsync`, `InvitationServiceTests.REG_INV_001_AC3_AcknowledgingAttachesTheMembershipThatWasShownAsync`, `InvitationServiceTests.REG_INV_001_AC4_AnIntegratedInvitationNeedsAPersonalEmailAsync`, `InvitationServiceTests.REG_INV_001_AC4_TheCorporateAddressBecomesPrimaryBesideThePersonalEmailAsync`, `MembershipAttachmentTests.REG_INV_001_AC3_TheMembershipCarriesTheAcknowledgementAndTheGrantsAsync`, `MembershipTests.REG_INV_001_AC3_TheMembershipCarriesTheAcknowledgement`, `RegistrationServiceTests.REG_INV_001_AC1_ABoundIdentifierCannotBeChangedAndAnOpenOneCanAsync`, `RegistrationServiceTests.REG_INV_001_AC2_TheAccountHoldsTheInvitationAndNoMembershipAsync` |
| REG-INV-002 | AC1, AC2, AC3 | `InvitationAcknowledgementFlowTests.REG_INV_002_AC3_AcknowledgingAttachesTheMembershipToTheSameAccountAsync`, `InvitationServiceTests.REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToThatAccountAsync`, `InvitationServiceTests.REG_INV_002_AC2_AnAccountBelowTheRequiredAssuranceIsHeldAtEnrolmentAsync`, `RegistrationFlowTests.REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToTheAccountAsync` |
| REG-MAIL-001 | AC1, AC2, AC3, AC4, AC5 | `IdentifierServiceTests.REG_MAIL_001_AC5_ThePersonalEmailStaysAsTheMembershipKeepsItAsync`, `IdentifierSetTests.REG_MAIL_001_AC5_ThePersonalEmailStaysVerifiedNonPrimaryAndNotified`, `InvitationEndpointTests.REG_MAIL_001_AC1_AStaffInvitationReservesTheMailboxAsync`, `InvitationServiceTests.REG_MAIL_001_AC1_ExpiryLeavesTheMailboxReservedUntilTheAddressIsInvitedAgainAsync`, `InvitationServiceTests.REG_MAIL_001_AC1_SendingAStaffInvitationCreatesADisabledMailboxAsync`, `InvitationServiceTests.REG_MAIL_001_AC1_TheSweepForgetsWhatAnExpiredInvitationBoundAsync`, `InvitationServiceTests.REG_MAIL_001_AC2_TheCorporateAddressIsSentNothingAsync`, `RegistrationServiceTests.REG_MAIL_001_AC3_ABoundPhoneIsVerifiedBeforeTheRegistrationGoesOnAsync`, `RegistrationServiceTests.REG_MAIL_001_AC4_ThePressVerifiesTheBoundEmailWithoutACodeAsync` |
| REG-MAIL-002 | AC1, AC2 | `AppPasswordsTests.REG_MAIL_002_AC1_TheServerGeneratesTheSecretAndTheLibraryKeepsNoneAsync`, `AppPasswordsTests.REG_MAIL_002_AC2_CreationAndRevocationAskForAStepUpAsync`, `ModelTests.REG_MAIL_002_AC1_NoTableHoldsAnAppPassword` |
| REG-MAIL-003 | AC1, AC2, AC3 | `IdentifierSetTests.REG_MAIL_003_AC2_ThePersonalEmailBecomesPrimaryAndTheCorporateAddressLeaves`, `IdentifierSetTests.REG_MAIL_003_AC3_MembershipEndNeverLeavesZeroVerifiedEmails`, `IdentifierStoreTests.REG_MAIL_003_AC1_TheRetiredAddressHasNoHolderToSignInAsAsync`, `InvitationServiceTests.REG_MAIL_003_AC2_EndingTheMembershipRetiresTheCorporateAddressAsync`, `MembershipEndingTests.REG_MAIL_003_AC3_EndingTheMembershipLeavesTheAccountsStateAsync` |
| IDN-LIFE-009b | AC1 to AC4, which the chapter states for IDN-LIFE-009a and IDN-LIFE-009b together | The tests carry IDN-LIFE-009a, listed above; the factor the organization's policy does not permit is refused by the session gate once the membership is in force, and nothing is written to the credential (`SessionServiceTests.IDN_LIFE_009a_AC3_APasswordHeldBeforeTheMembershipNoLongerAuthenticatesAsync`) |
| INT-MAIL-005 | AC1 | Verified by review: no source, response, error code or document of the library says staff are passkey-only. Chapter 00 section 3.1 ("passkeys only," as the example of a policy an organization carries) and `docs/guide/janus-explained.md` (the staff walk-through) use the words for the interactive policy without naming app passwords beside them |
| 09 sections 6a, 8, 8a | Every route but the three in section 2 | `SessionRequirementTests` names each route and the session it asks for |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| INT-MAIL-001 AC3 | That every provisioning and app-password operation is a JMAP request is a property of the mail server adapter, which no Milestone 1 package ships (entry 215) | Milestone 2 step 5 |
| INT-MAIL-004 AC1 | The mail server authenticating with introspection absent is the server's behaviour; the library's half, a discovery document advertising none, is AC2 | Milestone 2 step 5 |
| INT-MAIL-006 AC3 | Mail accepted at a newly provisioned address without a prior sign-in is the server's behaviour; the library pushes the mailbox before the membership begins (AC1) | Milestone 2 step 5 |
| INT-MAIL-006a AC2 | App-password authentication failing on a disabled account is the server's behaviour; the library pushes the disabled state on the first pass (AC1) | Milestone 2 step 5 |
| INT-MAIL-010 AC3, second half | A mail client failing on its next connection is the server's behaviour; the removal at the server is tested | Milestone 2 step 5 |
| INT-MAIL-002 AC1 | The mail server's connection string is the deployment's; the library opens its own database and no other (INT-MAIL-001 AC1, INT-MAIL-002 AC2) | Milestone 2 |
| INT-MAIL-006 AC1a | The bootstrap-created administrator's membership and queued provisioning | Phase 9, OPS-BOOT-001 |
| INT-MAIL-006 AC1b, the account half | The `emergency` account, which holds no address and no mailbox and for which no provisioning is attempted; that reconciliation does not flag its absence is tested | Phase 9, OPS-BOOT-002 |
| 09 section 8, `POST /auth/break-glass` and `POST /admin/break-glass/generate` | The break-glass credential and session | Phase 9, OPS-SEC |
| 09 section 8a, `PUT /admin/compliance/licences` | Licence and permit expiry warnings | Phase 9, OPS-MAINT-001 |
| The schedules of `InvitationService.SweepAsync`, `DomainReverification.SweepAsync`, `MailboxPublisher.PublishAsync` and `MailboxReconciliation.ReconcileAsync` | Each pass is built and tested; what runs it on its interval is the background job runner | Phase 9 |
| OPS-BOOT-002, `emergency` "cannot be granted anything further" | The refusal belongs in the grant operation once the reserved account exists; with it, whether the seeded `system-administrator` role stays editable while `emergency` holds it | Phase 9 |
| CONV-LOG-003 AC2 and CONV-LOG-004 | Phase 0 placed them in phase 5 and no report since lists them. In this phase JAN0002 was found to guard nothing: no type or member of the library carried `NeverLogged`. The app password's secret now does (INT-MAIL-010 AC4); the other carriers CONV-LOG-003 names (passwords, tokens, session identifiers, TOTP secrets and codes, recovery and verification codes, the screening prefix) are not yet marked | Phase 10, the exit-gate coverage pass |
| LIB-HOST-001 | `Settings.ThrowIfIncomplete` is called by no startup check, so a deployment missing a required key starts and fails at the first read of it | Phase 10 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `src/Janus.Hosting/Bff/ApiStatus.cs` | `config.key.protected` was answered 403 | `09` section 8, which lists it under 422 for `PUT /admin/config/{key}`, and `09` is authoritative for status codes | The map answers 422 |
| `src/Janus.Core/Configuration/PolicySetting.cs`, `SettingForms.cs` | A gate's age was written `maximumAge` | `10` section 4.1a, which names the gate fields `level`, `phishingResistant`, `maxAge` | The written form follows `10` section 4.1a (`SettingWrittenFormTests.Written_AGate_NamesItsFieldsAsThePolicyObjectDoes`) |
| `src/Janus.Core/Configuration/SettingForms.cs` | An organization's override naming `breakGlass` among its login factors was read | `10` section 4.1a, which says `breakGlass` "cannot appear here", as `PolicySetting` already refuses it for the system policy | The override form refuses it the same way (`SettingWrittenFormTests.Read_AnOverrideNamingTheEmergencyCredential_IsNotAllowed`) |
| `LibraryStructureTests.OPS_CFG_002_OnlyTheConfigurationAdministrationWritesARuntimeSetting` | The matcher saw a store write only where `.WriteAsync(` sat on the line of the field's name, so a call split over lines went unseen | OPS-CFG-002 and OPS-CFG-005, "Nothing else in the library calls the store's write", the working guide's section 3, a gate that mis-implements its own rule | The matcher admits whitespace before `.WriteAsync(`; it then found the organization policy key written from `OrganizationService`, which now goes through `ConfigurationAdministration` |
| `Janus.Hosting.Tests.csproj`, `Janus.Authorization.Tests.csproj` | The administration endpoints are tested at the browser boundary, and the authorization area's fakes were not reachable from there | the working guide's section 3, test infrastructure | The hosting test project references the authorization test project, which grants it `InternalsVisibleTo`, as the authentication and privacy test projects already do; no shipped project's surface changes |
| `tests/Janus.Hosting.Tests/Deployment.cs` | The harness exposed no fake for the policy raises, the domain store, the DNS resolver, the mailboxes or the mail server, named no sending domain, and captured no log line | the working guide's section 3, test infrastructure | The fakes are exposed; the sending domain is named and declared registered, as INT-MAIL-011 requires a deployment offering Apple to; every log line at every level is captured (`LogsInMemory`) for INT-MAIL-010 AC4 |
| `StartupValidationTests`, `HostFixture` | The startup checks could not complete once the relay warning ran among them: no `IEvents` was registered and no sending domain seeded | the working guide's section 3, test infrastructure; INT-MAIL-011, LIB-HOST-001 | The startup harness registers an `IEvents` fake and the fixture seeds `notification.email.sendingdomain`, as a deployment must |

## 4. Decided in the owner's absence

Under D-161 as amended by D-162. Each entry is in `docs/reports/decisions-pending-review.md`
in the same words, numbered as it is there.

| # | Decision |
|---|---|
| 168 | A takedown is identified by the outbox record its trigger writes |
| 169 | The takedown screen reads the latest takedown at `GET /admin/accounts/{subject}/takedown` |
| 170 | A takedown starts from active, restricted or suspended, and from nothing else |
| 171 | `AccountSuspended` is published after the trigger commits, and `TakedownExecuted` travels on the outbox |
| 172 | A reversal publishes `TakedownReversed` and nothing else |
| 173 | The reversal window closes at the start of the window plus `takedown.grace`, whether or not the sweep has run |
| 174 | A trigger and a reversal carry a reason, and the trigger one of the four spellings |
| 175 | An operation of the deployment asks for its permission in the administrative organization |
| 176 | A correlation identifier resolves for `audit:read` in the administrative organization, and for its own principal only on a disclosing type |
| 177 | The audit trail read by subject carries each record's codes and never what it holds under the subject's key |
| 178 | Every loosening of runtime configuration also needs `system:administer` |
| 179 | A change through the configuration route carries a reason whichever way it moves |
| 180 | The configuration route serves the deployment's own keys, the restriction set excepted |
| 181 | A configuration value crosses the interface in its own JSON type |
| 182 | A host key with no supplier is refused at the edit as a value the set does not admit |
| 183 | What the restriction routes read, and what they answer for a name the set does not hold |
| 184 | How a grant names the whole organization, and what else it must name |
| 185 | Which grants of system administration need system administration |
| 186 | What the grant routes answer beyond the rows 09 lists |
| 187 | What the role routes carry, and where `role:manage` is asked |
| 188 | A change to a role is reasoned, audited, and guarded as a grant of what it carries |
| 189 | A role a grant or a derivation names is not removed, and the refusal has a code of its own |
| 190 | What the group routes carry, and where `group:manage` is asked |
| 191 | A change of members is stepped up, reasoned, audited, and guarded as a grant of what the group holds |
| 192 | A group anything names is not removed, and the refusal has a code of its own |
| 193 | What the organization lifecycle routes carry, and the policy row a new organization gets |
| 194 | `organization:manage` is asked in the administrative organization |
| 195 | A deletion request and its cancellation are stepped up under a new gate `organization:delete` |
| 196 | A deletion request ends every member session, and a suspended organization's grants confer nothing |
| 197 | Every lifecycle change carries a reason and is audited under the organization |
| 198 | A cancellation after the window answers `identity.deletion.windowelapsed` |
| 199 | The resolved policy marks each field and each gate `{ value, overridden }` |
| 200 | Every change of an organization's policy is stepped up and reasoned |
| 201 | A loosening of an organization's policy also needs `system:administer` |
| 202 | `config.policy.belowsystem` names the first looser field as `details.field` |
| 203 | The administrative organization's policy never resolves below `aal2` |
| 204 | A replacement names only the policy's fields, never `emailDomains` |
| 205 | The domain lock is governed from the administrative organization, and verifying is a loosening |
| 206 | A lock whose listed domains are all unverified admits no address |
| 207 | A removed domain goes on refusing its addresses until it is listed anew |
| 208 | What the lock judges, when, and how a refusal is told |
| 209 | Verifying a domain, and the new code `identity.domain.unverified` |
| 210 | A failed scheduled check keeps the domain verified; only verified domains are re-checked |
| 211 | A domain's canonical form |
| 212 | The resolver is the host's; none is shipped |
| 213 | The system policy locks no domain |
| 214 | The domain endpoints' shapes and what each change writes down |
| 215 | The mail server is a port the host registers, and no adapter ships in Milestone 1 |
| 216 | Which organization's mail is integrated |
| 217 | The mailbox row is its own outbox, and the state owed is read, not published |
| 218 | A push is written down before it leaves, and a lost answer is never assumed |
| 219 | A push that spends its budget stays failed and is not retried |
| 220 | What reconciliation compares and what it reports |
| 221 | One mailbox per address, for good |
| 222 | The mailbox address is its holder's personal field |
| 223 | The invitation names its corporate address as `corporateEmail` |
| 224 | Which address the domain lock judges at issue, and what a refusal names |
| 225 | A phone the deployment does not collect is not bound |
| 226 | Where the link goes, and the token answered once where no email is bound |
| 227 | How the link is sent, and a link that cannot be sent issues nothing |
| 228 | The roles an invitation attaches ask what a grant asks |
| 229 | The documents an invitation attaches, at the version current when it is issued |
| 230 | An organization on its way out takes no invitation |
| 231 | One standing invitation per mailbox, and re-inviting an expired one |
| 232 | Issuing says nothing about who holds an account |
| 233 | What revoking takes, and what an acknowledged invitation answers |
| 234 | What an invitation row keeps, and for how long |
| 235 | Which language a message goes out in, and how a message in every language counts |
| 236 | The token a registration is begun with travels as `invitationToken` |
| 237 | What the press on the link verifies, and a bound email an account holds |
| 238 | A token that opens nothing, and a link spent by a registration never finished |
| 239 | A bound phone at its step |
| 240 | What of the inviting organization's policy governs the registration |
| 241 | Where a signed-in person's press on the link attaches the invitation |
| 242 | What the membership step reads, and the answer where nothing is attached |
| 243 | How the account keeps the personal email through the membership |
| 244 | What the acknowledgement names, and what it answers where the invitation no longer stands |
| 245 | Which identifiers must match at the acknowledgement |
| 246 | How the credential policy is met before the membership attaches |
| 247 | How the roles of an invitation are granted |
| 248 | What the corporate address does at the acknowledgement |
| 249 | How the acknowledgement is audited and exported |
| 250 | Where a membership is ended, and what it answers |
| 251 | What the end of a membership does to the corporate address |
| 252 | Whether the end of a membership removes the member's grants |
| 253 | What an erasure does to an invitation attached to the subject |
| 254 | What an administrator's suspension and reactivation answer and record |
| 255 | An administrator's suspension of an account its owner deactivated |
| 256 | What reactivation restores to an account suspended while restricted |
| 257 | A restriction held while the account is away from the restricted state |
| 258 | What lifting a restriction answers, writes and asks |
| 259 | Whether a restriction held away from the restricted state is lifted |
| 260 | What a deletion cancelled on the subject's behalf answers and records |
| 261 | What reading an account's photo as an administrator answers and whose policy withholds it |
| 262 | How the library obtains the person's token for the mail server |
| 263 | What the app-password endpoints answer and record |
| 264 | What an erasure's identifier is, what the erasure endpoints read, and what the manual completion records |
| 265 | What the "who can access this?" view reads, whom it answers, and what it answers over HTTP |
| 266 | A derivation confers nothing in a suspended organization |
| 267 | The audit trail by subject names what the subject did as well as what was done to it |
| 268 | The grants one user or group holds in its own name are read under `grant:read` |
| 269 | When and how the relay registration warning is raised |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The local
counts at the end of the phase: `Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests`
667, `Janus.Authorization.Tests` 114, `Janus.Core.Tests` 444, `Janus.Hosting.Tests` 411
and 140 integration, `Janus.Identity.Tests` 75, `Janus.Privacy.Tests` 183 and
`Janus.Storage.Tests` 32 and 316 integration, none failing.

The full gate was run once on the development machine: the integration suites above
against containers; the migrations applied from empty to two throwaway PostgreSQL 17
databases created as the double-migration job creates them, with no release tagged
yet so the second run also starts from empty, and applied again with nothing left to
apply; no model change without its migration; the contract suite; and the
forbidden-marker, acceptance-criterion test name, commit message and changelog jobs
over `main..HEAD`, all green.

The pipeline run, which is the gate of record, has not been made: the branch
`phase-08-organizations` is not pushed, and pushing waits on the owner's permission.
