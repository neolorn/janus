# Phase 3: Sessions, passwords, factors

Status: complete, full gate green, no open question. The session spine and its three
types, the assurance arithmetic, the lifetimes and their anchors, rotation, per-app
derivation, the session listing, passwords with the two floors and the screening that
never silently skips, Argon2id, the factor catalogue, time-based codes, WebAuthn with
the relying party settled at startup, recovery codes, trusted browsers, the new-device
check, the run-up when a requirement is raised, step-up gates with reachable assurance,
and the browser boundary with its cookies and its four refusal layers are built. The
decisions taken in the owner's absence are in section 4 and, in the same words, in
`docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTH-PRIN-001 | AC1, AC2, AC3 | `FailClosedTests.AUTH_PRIN_001_AC1_NothingIsDecidedFromACache`, `FailClosedTests.AUTH_PRIN_001_AC2_ABlocklistOutageDoesNotSkipScreeningAsync`, `FailClosedTests.AUTH_PRIN_001_AC3_NoPathReturnsAnAllowOnAnException` |
| AUTH-PRIN-002 | AC1, AC2, AC3, AC4 | `FactorCatalogueTests.AUTH_PRIN_002_AC1_NoEnumFlagOrClaimDistinguishesStaffFromCustomers`, `MembershipLookupTests.AUTH_PRIN_002_AC1_OnlyALiveMembershipResolvesAPolicyAsync`, `PolicyResolutionTests.AUTH_PRIN_002_AC2_TwoOrganizationsAreEnforcedIndependentlyAsync`, `MembershipLookupTests.AUTH_PRIN_002_AC3_APrincipalInNoOrganizationResolvesToNothingAsync`, `PolicyResolutionTests.AUTH_PRIN_002_AC3_APrincipalWithNoMembershipFollowsTheSystemPolicyAsync`, `PolicyResolutionTests.AUTH_PRIN_002_AC4_AnOrganizationOverridingNothingIsTheSystemPolicyAsync` |
| AUTH-FACT-001 | AC1, AC2, AC3, AC4, AC5, AC6 | `FactorCatalogueTests.AUTH_FACT_001_AC1_NoConditionalTestsForAFactorByName`, `AssuranceTests.AUTH_FACT_001_AC2_ANewRelayResistantFactorSatisfiesTheRulesUnchanged`, `AssuranceTests.AUTH_FACT_001_AC3_AVerificationOnlyFactorAuthenticatesNeither`, `AuthenticatorStoreTests.AUTH_FACT_001_AC4_TheRowCarriesWhatTheCredentialDidAsync`, `AuthenticatorStoreTests.AUTH_FACT_001_AC5_ALabelHeldTwiceForOneKindIsRefusedAsync`, `CredentialLabelTests.AUTH_FACT_001_AC6_TheDefaultLabelIsTheDeviceDescription` |
| AUTH-FACT-002 | AC1, AC2, AC2a, AC3, AC4, AC5 | `SessionServiceTests.AUTH_FACT_002_AC1_AnEntryOffByDefaultSignsInOnlyWhenNamedAsync`, `SessionServiceTests.AUTH_FACT_002_AC2_RemovingAnEntryBlocksItWithoutADeployAsync`, `FactorCatalogueTests.AUTH_FACT_002_AC2a_NoConfigurationKeyEnablesOrDisablesAFactor`, `SessionServiceTests.AUTH_FACT_002_AC3_DisablingAnEntryDeletesNoEnrolmentAsync`, `FactorCatalogueTests.AUTH_FACT_002_AC4_NoSignInPathAcceptsAVerificationCodeAsACredential`, `AssuranceTests.AUTH_FACT_002_AC5_APhoneLinkAloneRecordsAal1` |
| AUTH-FACT-002b | AC2 | `TotpServiceTests.AUTH_FACT_002b_AC2_ACodeGeneratorIsRefusedWithoutAPasswordAsync`, `WebAuthnServiceTests.AUTH_FACT_002b_AC2_ASecondStepIsRefusedWithoutAPasswordAsync` |
| AUTH-FACT-002a | AC1, AC2, AC3, AC4 | `SessionServiceTests.AUTH_FACT_002a_AC1_ASocialSignInYieldsAUsableSessionAtOnceAsync`, `SessionServiceTests.AUTH_FACT_002a_AC2_ASocialSignInIsNoSecondStepsFirst`, `SessionServiceTests.AUTH_FACT_002a_AC3_TheSessionRecordsDelegatedAsync`, `StepUpTests.AUTH_FACT_002a_AC4_TheProvidersCredentialIsInNoCombination` |
| AUTH-FACT-003 | AC2, AC3 | `AssuranceTests.AUTH_FACT_003_AC2_AnEmailFactorSignsInAtAal1AndProvesNothingAfterwards`, `AssuranceTests.AUTH_FACT_003_AC3_AnEmailFactorRaisesNothingBesideASecondFactor` |
| AUTH-FACT-004 | AC1 | `FactorCatalogueTests.AUTH_FACT_004_AC1_AVerificationCodeIsNoCatalogueEntry` |
| AUTH-FACT-005 | AC1, AC2, AC3 | `TotpServiceTests.AUTH_FACT_005_AC1_ACodeFromTwoStepsAgoIsRejectedAsync`, `TotpServiceTests.AUTH_FACT_005_AC2_ACodeFromOneStepAgoIsAcceptedAsync`, `TotpServiceTests.AUTH_FACT_005_AC3_TheSameCodeTwiceSucceedsOnceAsync` |
| AUTH-FACT-006 | AC1 | `AuthenticatorStoreTests.AUTH_FACT_006_AC1_TheSecretIsNotReadableFromTheTableAsync` |
| AUTH-FACT-007 | AC1 | `TotpServiceTests.AUTH_FACT_007_AC1_AnAbandonedEnrolmentLeavesNoActiveFactorAsync` |
| AUTH-FACT-008 | AC1, AC2, AC3, AC4, AC5 | `RecoveryCodeServiceTests.AUTH_FACT_008_AC1_AUsedCodeIsRejectedOnSecondPresentationAsync`, `RecoveryCodeStoreTests.AUTH_FACT_008_AC1_ASpentCodeIsSpentOnTheRowAsync`, `RecoveryCodeStoreTests.AUTH_FACT_008_AC2_TheTablesHoldNoColumnACodeCouldBeReadFromAsync`, `RecoveryCodeServiceTests.AUTH_FACT_008_AC3_ADumpOfTheTableYieldsNoUsableCodeAsync`, `RecoveryCodeStoreTests.AUTH_FACT_008_AC3_ADumpOfTheTableYieldsNoUsableCodeAsync`, `RecoveryCodeServiceTests.AUTH_FACT_008_AC4_TheSetRecordsWhenItWasShownAndExportedAsync`, `RecoveryCodeStoreTests.AUTH_FACT_008_AC4_TheTimestampsAreCarriedOnTheSetAsync`, `RecoveryCodeServiceTests.AUTH_FACT_008_AC5_AnOldSetRemindsItsOwnerOnceAsync` |
| AUTH-FACT-009 | AC1, AC2 | `RecoveryCodeServiceTests.AUTH_FACT_009_AC1_NoCodeOfThePreviousSetValidatesAsync`, `RecoveryCodeStoreTests.AUTH_FACT_009_AC1_ReplacingTheSetRemovesThePreviousCodesAsync`, `RecoveryCodeServiceTests.AUTH_FACT_009_AC2_TheRemainingCountIsVisibleAsync` |
| AUTH-FACT-010 | AC1, AC2, AC3 | `RelyingPartyTests.AUTH_FACT_010_AC1_AnIdentifierOfOneLabelIsRefused`, `RelyingPartyTests.AUTH_FACT_010_AC1_AnIdentifierOverNoConfiguredOriginIsRefused`, `RelyingPartyTests.AUTH_FACT_010_AC1_AnIdentifierOverOnlySomeOriginsIsRefused`, `RelyingPartyTests.AUTH_FACT_010_AC1_AnOriginThatIsNotAnOriginIsRefused`, `RelyingPartyTests.AUTH_FACT_010_AC2_TheDerivedIdentifierFollowsLabelBoundaries`, `RelyingPartyTests.AUTH_FACT_010_AC2_TheDerivedIdentifierIsTheCommonParent`, `WebAuthnServiceTests.AUTH_FACT_010_AC3_ARefusedConfigurationEnrolsNothingAsync` |
| AUTH-FACT-011 | AC1, AC2 | `RelyingPartyTests.AUTH_FACT_011_AC1_ACredentialUnderAPreviousIdentifierIsDetected`, `WebAuthnServiceTests.AUTH_FACT_011_AC1_ACredentialUnderAPreviousIdentifierIsDetectedAsync`, `WebAuthnServiceTests.AUTH_FACT_011_AC2_WhatIsStaleIsAnsweredFromTheRecordsAloneAsync` |
| AUTH-FACT-012 | AC1, AC2 | `RelyingPartyTests.AUTH_FACT_012_AC1_TheDocumentListsExactlyTheConfiguredOrigins`, `RelyingPartyTests.AUTH_FACT_012_AC2_FiveDistinctLabelsStand`, `RelyingPartyTests.AUTH_FACT_012_AC2_MoreThanFiveDistinctLabelsIsRefused`, `RelyingPartyTests.AUTH_FACT_012_AC2_SubdomainsOfOneDomainCountOnce` |
| AUTH-FACT-013 | AC1, AC2 | `WebAuthnServiceTests.AUTH_FACT_013_AC1_BackupEligibilityAndStateAreRecordedAsync`, `WebAuthnServiceTests.AUTH_FACT_013_AC2_ASyncedCredentialIsToldApartFromADeviceBoundOneAsync` |
| AUTH-FACT-014 | AC1, AC2, AC3, AC4, AC5 | `WebAuthnServiceTests.AUTH_FACT_014_AC1_AnAssertionWithoutUserVerificationIsRejectedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC1_AnEnrolmentWithoutUserVerificationIsRefusedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC2_AnUnattestedCredentialEnrolsAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC3_ACounterMovingBackwardsIsRejectedAndAuditedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC3_ACounterStandingStillIsRejectedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC3_ACounterThatAdvancedIsRecordedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC4_AnAlgorithmOutsideTheAllowListIsRefusedAsync`, `WebAuthnServiceTests.AUTH_FACT_014_AC4_TheAllowListIsWhatTheDeploymentConfiguredAsync`, `SettingsTests.AUTH_FACT_014_AC5_TheAllowListHoldsItsThreeAlgorithmsAndKeepsOne` |
| AUTH-FACT-015 | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | `DeviceServiceTests.AUTH_FACT_015_AC1_ATrustedBrowserSkipsTheSecondFactorAsync`, `DeviceServiceTests.AUTH_FACT_015_AC2_TheTrustRaisesNothingOnTheSessionAsync`, `DeviceServiceTests.AUTH_FACT_015_AC3_TheOfferIsAbsentUnderAPolicyRequiringTwoFactors`, `DeviceServiceTests.AUTH_FACT_015_AC4_EveryTrustedDeviceGoesAtOnceAsync`, `DeviceServiceTests.AUTH_FACT_015_AC5_ATrustOlderThanItsLifetimeStandsForNothingAsync`, `DeviceServiceTests.AUTH_FACT_015_AC5_TheLifetimeIsWhatTheDeploymentConfiguresAsync`, `DeviceServiceTests.AUTH_FACT_015_AC6_TheFailureLimitIsWhatTheDeploymentConfiguresAsync`, `DeviceServiceTests.AUTH_FACT_015_AC6_ThreeFailuresInARowRevokeTheTrustAsync`, `DeviceServiceTests.AUTH_FACT_015_AC7_TheOfferIsWithheldBelowTheSingleFactorFloor` |
| AUTH-FACT-016 | AC1, AC2, AC3, AC6 | `DeviceServiceTests.AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync`, `DeviceServiceTests.AUTH_FACT_016_AC2_ARememberedBrowserIsNotHeldAgainAsync`, `DeviceServiceTests.AUTH_FACT_016_AC2_TheRememberedPeriodIsWhatTheDeploymentConfiguresAsync`, `DeviceServiceTests.AUTH_FACT_016_AC3_ATwoFactorSignInIsNeverHeldAsync`, `DeviceServiceTests.AUTH_FACT_016_AC6_WithTheCheckOffNoSignInIsHeldAsync` |
| AUTH-FACT-017 | AC1, AC2, AC3, AC4 | `PolicyGraceTests.AUTH_FACT_017_AC1_WithNoRunUpTheRequirementHoldsAtOnce`, `PolicyGraceTests.AUTH_FACT_017_AC2_AnAccountThatMeetsItIsHeldByNothing`, `PolicyGraceTests.AUTH_FACT_017_AC2_InsideTheRunUpTheSignInContinues`, `PolicyGraceTests.AUTH_FACT_017_AC3_AnAccountCreatedAfterTheChangeIsHeldAtOnce`, `PolicyGraceTests.AUTH_FACT_017_AC4_LoweringARequirementRaisesNothing` |
| AUTH-PASS-001 | AC1, AC2, AC3 | `PasswordFloorTests.AUTH_PASS_001_AC1_AFifteenCharacterPasswordIsAcceptedForASingleFactorAccount`, `PasswordFloorTests.AUTH_PASS_001_AC1_AFourteenCharacterPasswordIsRefusedForASingleFactorAccount`, `PasswordFloorTests.AUTH_PASS_001_AC2_ATenCharacterPasswordIsAcceptedWhereASecondFactorIsHeld`, `PasswordFloorTests.AUTH_PASS_001_AC3_LengthIsCountedInCharactersAndNotInBytes`, `PasswordFloorTests.AUTH_PASS_001_AC3_TheMaximumIsAcceptedAndOneBeyondItIsRefused` |
| AUTH-PASS-001a | AC2, AC3, AC4 | `PasswordFloorTests.AUTH_PASS_001a_AC2_ATenCharacterPasswordIsRefusedWhereNoSecondFactorIsHeld`, `PasswordServiceTests.AUTH_PASS_001a_AC2_TheShorterFloorIsReachedByHoldingASecondFactorAsync`, `PasswordFloorTests.AUTH_PASS_001a_AC3_TheFlagFollowsTheSingleFactorFloor`, `PasswordServiceTests.AUTH_PASS_001a_AC3_TheFloorFlagIsRecordedAndFollowsEveryChangeAsync`, `PasswordStoreTests.AUTH_PASS_001a_AC3_AChangeUpdatesTheFlagAsync`, `PasswordStoreTests.AUTH_PASS_001a_AC3_TheFloorFlagIsStoredWithTheHashAsync`, `DeviceServiceTests.AUTH_PASS_001a_AC4_TheOfferIsAbsentBelowTheFloor` |
| AUTH-PASS-002 | AC1 | `PasswordFloorTests.AUTH_PASS_002_AC1_AnAllLowercasePasswordMeetingTheFloorIsAccepted` |
| AUTH-PASS-003 | AC1 | `PasswordStoreTests.AUTH_PASS_003_AC1_TheTableCarriesNoExpiryFieldAsync` |
| AUTH-PASS-004 | AC1, AC2, AC3, AC4, AC5 | `PasswordScreeningTests.AUTH_PASS_004_AC1_AKnownBreachedPasswordIsRefusedWhateverItsLengthAsync`, `ScreeningTests.AUTH_PASS_004_AC2_TheOfflineListAcceptsWhatItDoesNotHoldAsync`, `PasswordServiceTests.AUTH_PASS_004_AC3_APasswordThatCouldNotBeScreenedIsNotRecordedAsync`, `PasswordScreeningTests.AUTH_PASS_004_AC4_AtTheDefaultSourcesAnOwnWordDoesNotRefuseAsync`, `PasswordScreeningTests.AUTH_PASS_004_AC5_WithContextEnabledTheOwnWordRefusesAsync`, `PasswordServiceTests.AUTH_PASS_004_AC5_AProfileFieldMatchingThePasswordPromptsAtTheNextSignInAsync` |
| AUTH-PASS-005 | AC1, AC2 | `PasswordAdviceTests.AUTH_PASS_005_AC1_APoorlyScoringPasswordIsReportedAndNotRefused`, `PasswordAdviceTests.AUTH_PASS_005_AC2_TheFeedbackNamesTheOwnWordThePasswordHolds` |
| AUTH-PASS-006 | AC1, AC2 | `PasswordProhibitionTests.AUTH_PASS_006_AC1_NoHintOrQuestionFieldExistsInTheSchema`, `PasswordProhibitionTests.AUTH_PASS_006_AC2_NoApiAcceptsASecurityQuestionOrAnswer` |
| AUTH-PASS-007 | AC1, AC2, AC3 | `Argon2idHasherTests.AUTH_PASS_007_AC1_RaisingTheParametersLeavesExistingPasswordsVerifiable`, `PasswordServiceTests.AUTH_PASS_007_AC1_AnUnchangedDeploymentRehashesNothingAsync`, `PasswordServiceTests.AUTH_PASS_007_AC2_ASignInAfterARaiseUpgradesTheHashAsync`, `PasswordStoreTests.AUTH_PASS_007_AC2_ARehashLeavesEverythingButTheHashAsync`, `Argon2idHasherTests.AUTH_PASS_007_AC3_TheDefaultHashesAtTheShippedParameters` |
| AUTH-SESS-001 | AC1, AC2 | `SessionStoreTests.AUTH_SESS_001_AC1_EndingTheRecordEndsWhatDerivesFromItAsync`, `SessionStoreTests.AUTH_SESS_001_AC2_TheRecordReloadsFromTheDurableStoreAsync` |
| AUTH-SESS-002 | AC1, AC2, AC3 | `SessionServiceTests.AUTH_SESS_002_AC1_NoSessionFieldNamesAFactor`, `SessionServiceTests.AUTH_SESS_002_AC2_TheAuditRecordNamesTheFactorAsync`, `AssuranceTests.AUTH_SESS_002_AC3_AStepUpRuleIsALevelAResistanceAndAnAge` |
| AUTH-SESS-003 | AC1, AC2, AC3 | `BrowserCookieTests.AUTH_SESS_003_AC1_NoSessionTokenIsReadableByAScript`, `BrowserCookieTests.AUTH_SESS_003_AC2_TheCookieCarriesAllFourAttributes`, `SessionServiceTests.AUTH_SESS_003_AC3_TheCookieValueYieldsNothingWhenDecodedAsync`, `SessionStoreTests.AUTH_SESS_003_AC3_TheRowCarriesTheFingerprintAndNotTheSecretAsync` |
| AUTH-SESS-004 | AC1, AC2, AC3 | `SessionServiceTests.AUTH_SESS_004_AC1_ACompromisedApplicationsSessionOpensNoOtherAsync`, `SessionServiceTests.AUTH_SESS_004_AC2_AnotherApplicationDerivesWithoutReauthenticatingAsync`, `SessionServiceTests.AUTH_SESS_004_AC3_RevokingTheRecordTerminatesWhatDerivesFromItAsync` |
| AUTH-SESS-012 | AC7 | `SessionServiceTests.AUTH_SESS_012_AC7_TheDerivedSessionStandsOnTheRecordAsync` |
| AUTH-SESS-005a | AC1, AC2, AC2a, AC2b, AC3 | `AssuranceTests.AUTH_SESS_005a_AC1_EveryCombinationReachesWhatTheTableAssigns`, `SessionServiceTests.AUTH_SESS_005a_AC1_APasskeySessionRecordsAal2AndKeepsItsPolicysLifetimeAsync`, `AssuranceTests.AUTH_SESS_005a_AC2_ASocialOnlySessionRecordsDelegated`, `SessionServiceTests.AUTH_SESS_005a_AC2a_ASecondFactorLeavesADelegatedSessionDelegatedAsync`, `AssuranceTests.AUTH_SESS_005a_AC2b_APasswordAndAnSmsCodeReachAal2WithoutRelayResistance`, `AssuranceTests.AUTH_SESS_005a_AC3_NoLevelIsDerivedFromAnExternalClaim` |
| AUTH-SESS-005b | AC1, AC2, AC3 | `SessionServiceTests.AUTH_SESS_005b_AC1_AStaffSessionBelowAal2IsRefusedAsync`, `SessionServiceTests.AUTH_SESS_005b_AC2_AFurtherPrimaryFactorDoesNotLowerTheFloorAsync`, `SessionServiceTests.AUTH_SESS_005b_AC3_ABreakGlassSessionIsEstablishedDespiteTheFloorAsync` |
| AUTH-SESS-005 | AC1, AC2, AC2a, AC3, AC3a, AC4, AC5, AC6 | `SessionServiceTests.AUTH_SESS_005_AC1_ACustomerSessionInUseExpiresAtTheAbsoluteLimitAsync`, `SessionServiceTests.AUTH_SESS_005_AC2_ACustomerSessionHasOneLifetimeWhateverSignedItInAsync`, `SessionServiceTests.AUTH_SESS_005_AC2a_ASecondFactorAloneRestoresNothingAsync`, `SessionServiceTests.AUTH_SESS_005_AC3_AStaffSessionExpiresAfterAnHourIdleOrADayAsync`, `SessionServiceTests.AUTH_SESS_005_AC3a_ACustomerIdleExpiryAsksForAFullAuthenticationAsync`, `SessionServiceTests.AUTH_SESS_005_AC4_OneFactorRestoresTheSameSessionInsideTheWindowAsync`, `SettingsTests.AUTH_SESS_005_AC5_AnAal2AbsoluteOfFortyEightHoursIsRejected`, `SessionServiceTests.AUTH_SESS_005_AC6_AnOrganizationsShorterTimeoutAppliesAsync` |
| AUTH-SESS-006 | AC1, AC2 | `SessionServiceTests.AUTH_SESS_006_AC1_APrivilegeChangeIssuesANewSecretAsync`, `SessionServiceTests.AUTH_SESS_006_AC2_ThePreviousSecretIsInvalidatedNotOrphanedAsync` |
| AUTH-SESS-007 | AC1, AC2, AC3 | `BrowserProfileTests.AUTH_SESS_007_AC1_AStateChangeWithoutAValidTokenIsRejectedAsync`, `BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut`, `BrowserProfileTests.AUTH_SESS_007_AC3_TheRejectionIsLoggedAsync` |
| AUTH-SESS-008 | AC1, AC2 | `SessionServiceTests.AUTH_SESS_008_AC1_SigningOutEverywhereLeavesNoSessionActiveAsync`, `SessionServiceTests.AUTH_SESS_008_AC2_AnotherApplicationRequiresAuthenticationAfterLogoutAsync` |
| AUTH-SESS-013 | AC1, AC2, AC3, AC4 | `SessionServiceTests.AUTH_SESS_013_AC1_TheListingMarksExactlyOneSessionCurrentAsync`, `SessionServiceTests.AUTH_SESS_013_AC2_EachEntryCarriesTimesDeviceAndCityAsync`, `SessionServiceTests.AUTH_SESS_013_AC3_EndingOneSessionLeavesTheOthersIntactAsync`, `SessionStoreTests.AUTH_SESS_013_AC4_TheLocationIsNotReadableFromTheTableAsync`, `SessionStoreTests.AUTH_SESS_013_AC4_TheLocationIsUnreadableAfterErasureAsync` |
| AUTH-SESS-010 | AC1, AC3 | `SessionServiceTests.AUTH_SESS_010_AC1_EndingAnAccountRefusesTheNextRequestAsync`, `SubjectEraserTests.AUTH_SESS_010_AC3_DeletionEndsSessionsBeforeThePersonalDataGoesAsync` |
| AUTH-SESS-011 | AC1, AC2 | `SessionServiceTests.AUTH_SESS_011_AC1_RevokingOneAccountLeavesTheOthersIntactAsync`, `SessionServiceTests.AUTH_SESS_011_AC2_RevokingOneAccountWithoutThePermissionIsRefusedAsync` |
| AUTH-SESS-009 | AC1, AC2, AC3, AC4 | `SessionServiceTests.AUTH_SESS_009_AC1_TighteningThePolicyEndsNoLiveSessionAsync`, `StepUpTests.AUTH_SESS_009_AC2_ASessionBelowTheRaisedRequirementIsAskedAgain`, `SessionServiceTests.AUTH_SESS_009_AC3_TheExplicitRevocationTerminatesEverySessionAsync`, `PolicyGraceTests.AUTH_SESS_009_AC4_TheEndOfTheRunUpEndsNoSession` |
| AUTH-STEP-001 | AC1, AC2, AC3 | `StepUpGatesTests.AUTH_STEP_001_AC1_ExercisingABoundPermissionAsksForStepUp`, `StepUpGatesTests.AUTH_STEP_001_AC2_SplittingAnApplicationChangesNothing`, `StepUpTests.AUTH_STEP_001_AC3_ASecondOrganizationSetsItsOwnGateIndependently` |
| AUTH-STEP-002 | AC1, AC2, AC3, AC4, AC4a, AC4b, AC5, AC6, AC7, AC8 | `StepUpTests.AUTH_STEP_002_AC1_AGateIsThreeValuesAndNamesNoFactor`, `StepUpTests.AUTH_STEP_002_AC2_TheDecisionReadsTheSessionAndWhatIsReachable`, `StepUpTests.AUTH_STEP_002_AC3_ASessionThatAlreadyProvedItIsNotChallenged`, `StepUpTests.AUTH_STEP_002_AC3_EvidenceOlderThanTheMaximumAgeIsNotEnough`, `AssuranceTests.AUTH_STEP_002_AC4_AnOwnCombinationProvesWhatItReaches`, `StepUpTests.AUTH_STEP_002_AC4_EveryCombinationThatReachesTheGateIsOffered`, `StepUpTests.AUTH_STEP_002_AC4a_AnSmsCodePassesOnlyWhereRelayResistanceIsNotAsked`, `StepUpTests.AUTH_STEP_002_AC4b_OnlyRelayResistantCombinationsPassSuchAGate`, `StepUpTests.AUTH_STEP_002_AC5_APasswordOnlyAccountPassesItsOwnGate`, `StepUpTests.AUTH_STEP_002_AC6_NothingThatFallsShortIsOffered`, `StepUpTests.AUTH_STEP_002_AC7_APendingLossReportIsShownWithItsCompletion`, `StepUpTests.AUTH_STEP_002_AC7_AnAccountBelowTheGateIsToldToEnrol`, `StepUpTests.AUTH_STEP_002_AC7_AnAccountThatCannotPresentItIsToldToReportALoss`, `StepUpTests.AUTH_STEP_002_AC8_RegisteringANewFactorTypeChangesNoGate` |
| AUTH-STEP-002a | AC1, AC2, AC3 | `PolicyResolutionTests.AUTH_STEP_002a_AC1_APrincipalWithNoMembershipResolvesToTheSystemPolicyAsync`, `PolicyResolutionTests.AUTH_STEP_002a_AC2_TheAdministrativeOrganizationsPolicyDoesNotReachACustomerAsync`, `PolicyTests.AUTH_STEP_002a_AC3_TheAdministrativePolicyGatesEveryStepUpAction`, `PolicyTests.AUTH_STEP_002a_AC3_TheSystemPolicyGatesEveryStepUpAction` |
| AUTH-STEP-006 | AC1, AC2, AC3 | `StepUpTests.AUTH_STEP_006_AC1_WhatAnAccountReachesIsWhatItsFactorsReach`, `StepUpTests.AUTH_STEP_006_AC2_ASuspendedFactorStillStands`, `StepUpTests.AUTH_STEP_006_AC3_RecoveryCodesChangeNothingAboutWhatIsReachable` |
| AUTH-STEP-007 | AC2 | `StepUpTests.AUTH_STEP_007_AC2_AnAccountReachingTwoFactorsPresentsTwo`, `StepUpTests.AUTH_STEP_007_AC2_EnrolmentIsGatedAtTheLowerOfTheTwo` |
| AUTH-STEP-004 | AC1, AC3 | `StepUpTests.AUTH_STEP_004_AC1_ABreakGlassSessionSatisfiesEveryGate`, `StepUpTests.AUTH_STEP_004_AC3_TheExceptionDoesNotOutliveTheSession` |
| AUTH-STEP-005 | AC1, AC2, AC3 | `StepUpTests.AUTH_STEP_005_AC1_ASocialCredentialSatisfiesNoGate`, `StepUpTests.AUTH_STEP_005_AC2_ASocialOnlySubjectIsToldToEnrol`, `StepUpTests.AUTH_STEP_005_AC3_WhatTheProviderAssertedDecidesNothing` |
| AUTH-STEP-003 | AC1, AC2 | `StepUpGatesTests.AUTH_STEP_003_AC1_WithNoAssuranceProviderABoundPermissionIsDenied`, `StepUpGatesTests.AUTH_STEP_003_AC2_TheDenialIsDistinguishableFromAnOrdinaryOne` |
| INT-PWD-001 | AC1, AC2 | `PasswordScreeningTests.INT_PWD_001_AC1_OnlyAPrefixLeavesTheDeploymentAsync`, `ScreeningTests.INT_PWD_001_AC1_TheRequestCarriesThePrefixOnlyAsync`, `ScreeningTests.INT_PWD_001_AC2_NoFullHashAppearsInTheRequestAsync` |
| INT-PWD-002 | AC1, AC2 | `PasswordScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheFallbackRunsAndIsRecordedAsync`, `ScreeningTests.INT_PWD_002_AC1_WithTheServiceUnreachableTheOfflineListAnswersAsync`, `PasswordScreeningTests.INT_PWD_002_AC2_WithBothUnavailableTheOperationFailsAsync`, `ScreeningTests.INT_PWD_002_AC2_WithNeitherAvailableTheOperationFailsAsync` |
| INT-PWD-003 | AC1 | `PasswordScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsAConfigurationChangeAsync`, `ScreeningTests.INT_PWD_003_AC1_SwitchingToTheSelfHostedCorpusIsConfigurationOnlyAsync` |
| BFF-SESS-001 | AC1, AC2 | `BrowserCookieTests.BFF_SESS_001_AC1_NothingButTheTwoOpaqueValuesReachesTheBrowser`, `BrowserCookieTests.BFF_SESS_001_AC2_TheCookieValueYieldsNothingWhenDecoded` |
| BFF-SESS-002 | AC1, AC2 | `BrowserCookieTests.BFF_SESS_002_AC1_EveryIssueCarriesTheFourAttributes`, `BrowserCookieTests.BFF_SESS_002_AC2_NoConfigurationKeyWeakensAnAttribute` |
| BFF-SESS-003 | AC1, AC3 | `BrowserCookieTests.BFF_SESS_003_AC1_NoCookieIsScopedToAParentDomain`, `BrowserProfileTests.BFF_SESS_003_AC3_EndingTheRecordTerminatesEveryApplicationsSessionAsync` |
| BFF-SESS-004 | AC1, AC2 | `BrowserProfileTests.BFF_SESS_004_AC1_RotationReplacesTheIdentifierAndInvalidatesItAsync`, `BrowserProfileTests.BFF_SESS_004_AC2_ThePreviousIdentifierIsInvalidatedNotOrphanedAsync` |
| BFF-SESS-005 | AC1 | `BrowserCookieTests.BFF_SESS_005_AC1_SigningOutClearsBothCookies` |
| BFF-CSRF-001 | AC1, AC2, AC3 | `BrowserProfileTests.BFF_CSRF_001_AC1_AStateChangeWithoutASessionBoundTokenIsRejectedAsync`, `SessionStoreTests.BFF_CSRF_001_AC1_TheRowCarriesTheTokenBoundToTheSessionAsync`, `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute`, `BrowserProfileTests.BFF_CSRF_001_AC3_AddingAnEndpointInheritsEnforcementAsync` |
| BFF-CSRF-002 | AC1, AC2, AC3 | `BrowserProfileTests.BFF_CSRF_002_AC1_ACrossSitePostIsRejectedBeforeAnyEndpointAsync`, `BrowserProfileTests.BFF_CSRF_002_AC2_WithNoFetchMetadataTheTokenIsStillValidatedAsync`, `BrowserProfileTests.BFF_CSRF_002_AC3_SameSiteNavigationIsUnaffectedAsync` |
| BFF-CSRF-003 | AC1 | `BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync` |
| BFF-CSRF-004 | AC1 | `BrowserProfileTests.BFF_CSRF_004_AC1_AMismatchedOriginIsRejectedAndLoggedAsync` |
| BFF-CSRF-005 | AC1, AC2, AC3, AC4 | `BrowserCookieTests.BFF_CSRF_005_AC1_TheManagementApplicationsCookieIsStrict`, `BrowserCookieTests.BFF_CSRF_005_AC2_APublicApplicationCarriesItsSessionOnAnInboundLink`, `BrowserCookieTests.BFF_CSRF_005_AC3_NoCookieIsIssuedWithSameSiteNone`, `BrowserProfileTests.BFF_CSRF_005_AC4_AReturnByNavigationKeepsTheSessionAsync` |
| BFF-CSRF-006 | AC1, AC2 | `BrowserCookieTests.BFF_CSRF_006_AC1_TheTokenIsSetBesideTheSessionAndReadableByScript`, `BrowserProfileTests.BFF_CSRF_006_AC2_RotationInvalidatesThePreviousTokenAsync` |
| BFF-CSRF-007 | AC1 | `BrowserProfileTests.BFF_CSRF_007_AC1_TheTokenIsDrawnAndComparedByTheFramework` |

Two criteria phase 2 left to this one because there was no session to decide them
against are decided here:
`SessionServiceTests.AUTHZ_SCOPE_001_AC1_NoSessionFieldNamesAnOrganization` and
`BrowserCookieTests.AUTHZ_CACHE_002_AC1_NoPermissionOrRoleNameIsInACookie`.

AUTH-STEP-008 states its seven invariants in place of acceptance criteria and requires
each to be written as a test. They are numbered here as the item numbers them:
`FactorCatalogueTests.AUTH_STEP_008_AC1_NoGateRuleNamesAFactor`,
`StepUpTests.AUTH_STEP_008_AC2_TwoAccountsReachingTheSameAreAskedTheSame`,
`StepUpTests.AUTH_STEP_008_AC3_EveryAccountStateAndGateHasAMove`,
`StepUpTests.AUTH_STEP_008_AC4_RemovingAnAuthenticatorIsNotGatedOnPresentingIt`,
`StepUpTests.AUTH_STEP_008_AC5_ReachableAssuranceFallsOnlyOnInvalidation`,
`StepUpTests.AUTH_STEP_008_AC6_NothingProvesMoreThanWhatWasPresented`,
`StepUpTests.AUTH_STEP_008_AC7_OneAnchorDecidesWhatIsAsked`. Invariant 5's second half,
the notification that accompanies a fall in reachable assurance, is AUTH-RECOV-007's
and waits with it.

The vocabularies of `10` sections 5.2, 5.3a and 1.2 that this phase adds are guarded by
`VocabularyContractTests.LIB_API_001_AC2_TheSessionVocabulariesAreTheContract` and
`VocabularyContractTests.LIB_API_001_AC2_TheFactorIdentifiersAreTheContract`; the
mounted pipeline by `BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration`
and `BrowserProfileTests.BFF_OWN_003_AC2_TheMountedStagesRunInTheContractsOrderAsync`.

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| AUTH-FACT-001 | AC6, second half | Inspection: `PATCH /account/credentials/{id}` is the account API of `09` section 4.5, which phase 5 builds. The half this phase owns, that an enrolment supplying no label takes the client's description of the device, is decided by `CredentialLabelTests.AUTH_FACT_001_AC6_TheDefaultLabelIsTheDeviceDescription` |
| AUTH-PASS-004 | AC1, the corpus itself | By construction and by test: the range API is called with five characters of the hash (`ScreeningTests.INT_PWD_001_AC1_TheRequestCarriesThePrefixOnlyAsync`) and the offline list is a file the deployment names. No corpus file is committed; D-153 leaves the licence of the published list to the owner, and the tests run against a fake that honours the contract, as the plan directs for every external |
| BFF-CSRF-003 | AC2 | The frontend's obligation (`18` FE-API-002), verified negatively here: a state-changing request without the header is refused whatever else it carries (`BrowserProfileTests.BFF_CSRF_003_AC1_ARequestWithoutTheCustomHeaderIsRejectedAsync`), so a frontend that does not set it cannot work at all |
| BFF-CSRF-005 | AC4, the provider's half | Inspection: the library has no payment provider and states none (the working guide's section 4, generic library). What it owns is decided by `BrowserProfileTests.BFF_CSRF_005_AC4_AReturnByNavigationKeepsTheSessionAsync`: a return arriving as a top-level navigation keeps the session and reaches the application, and one arriving as a cross-site state change does not, which is the reason the chapter gives for landing a posting return on a reading route |
| AUTH-FACT-016 | AC1, the emailed code | The code is a verification code (AUTH-FACT-004), whose storage and lifetime belong with the registration session; the hold itself, `auth.device.verificationrequired` and the browser's remembering are decided by `DeviceServiceTests.AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync` |

One analyser suppression exists in the phase, with its justification on the same line:
`src/Janus.Authentication/Passwords/PasswordScreening.cs` line 150 suppresses CA5350
because the published corpus is indexed by SHA-1 prefix (INT-PWD-001) and the value is
a lookup key, never a security claim.

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTH-FACT-002b AC1, AC3 to AC6 | Discoverable-credential sign-in and the second-factor-to-passkey upgrade are the item's own subject and the plan places them with the recovery and sign-in-link work. AC2, that a second step is refused to an account holding no password, is decided here | Phase 6 |
| AUTH-FACT-002a AC5 | Linking a provider is `/account/credentials/*`, the account API | Phase 5 |
| AUTH-FACT-003 AC1, AC4, AC5 | Sign-in links and `emailCode` are built with recovery | Phase 6 |
| AUTH-FACT-004 AC2, AC3 | A verification code has its own storage, lifetime and attempt cap, which the registration session owns; AC1, that no catalogue entry is one, is decided here | Phase 5 |
| AUTH-FACT-016 AC4 | The code counts against `email.destination`, which is the restriction model | Phase 4, AUTH-ABUSE-004 |
| AUTH-FACT-016 AC5 | `DeviceVerified` is an emitted event, and no event surface exists yet | Phase 4; section 4, decision 18 |
| AUTH-FACT-016 AC7 | The browser that completed the terms step is recorded by the registration flow | Phase 5, REG-SESS-007 |
| AUTH-PASS-001a AC1 | A registration that never reaches `active` is the registration flow; the floor that refuses the password is decided here | Phase 5 |
| AUTH-SESS-010 AC2 | A takedown is the two-phase operation of phase 8; AC1 and AC3 are decided here | Phase 8 |
| AUTH-SESS-012 AC1 to AC6 | The OIDC provider; AC7, that a derived session stands on the record, is decided here | Phase 6 |
| AUTH-STEP-007 AC1 | The enrolment notification to every recorded channel | Phase 4 |
| AUTH-STEP-007 AC3 | A recovered password is the recovery flow | Phase 6 |
| AUTH-STEP-004 AC2 | Break-glass sessions are issued by the operations code, which audits what they do; AC1 and AC3 are decided here | Phase 9, OPS-BOOT-002 |
| INT-PWD-004 AC1 | The call appears in generated records, which the RoPA generator writes | Phase 7, PRIV-ROPA-003 |
| BFF-SESS-003 AC2 | Navigation between applications re-establishes through the authentication application, which is the OIDC flow | Phase 6, BFF-SESS-006 |
| BFF-SESS-006 AC1 to AC5 | The OIDC provider and the confidential client | Phase 6 |
| BFF-CSRF-005a, BFF-CSRF-005b | The pre-authentication session and the registration session bound to it, which the plan places with registration | Phase 5 |
| Registration of the authentication services in `AddJanus` | Every service of the area reads `IConfigurationStore`, which has no implementation: no type reads the `settings` table, and giving one a way to read a stored value back into a typed setting is a public-surface addition that belongs with the runtime edits | Phase 4; section 4, decision 19 |
| INT-GEN-006 | The city shown on a session is resolved from a local database refreshed by a background job. The session records the place the caller supplies and degrades to none, which AUTH-SESS-013 is decided against | Section 4, decision 20 |

`IMembershipLookup`, the other port the area needs, is implemented here over the
`memberships` table and registered.

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `src/Janus.Core/Configuration/ConfigurationKey.cs` | The key shape admitted only a letter at the head of every segment, so no family key whose parameter is an organization identifier parsed | `10` section 4, D-151: a family key carries the organization identifier as its last segment | The shape admits what D-151 says the segment is, and an identifier is a version 7 value that begins with a digit about half the time |
| `src/Janus.Authentication/Janus.Authentication.csproj`, `src/Janus.Authorization`, `src/Janus.Identity`, `src/Janus.Privacy`, `tests/Janus.Authentication.Tests`, and `LibraryStructureTests.CONV_LAYOUT_002_AC1` | `Janus.Hosting.Tests` could not see the internals its subject is written in, nor the fakes the area already owns | CONV-TEST-002, CONV-LAYOUT-002, the working guide's section 3, Tier 1 test infrastructure | An area that opens its internals to `Janus.Hosting` opens them to that project's tests for the same reason, and the gate test carries the same list |
| `src/Janus.Storage/Authentication/Passwords/PasswordConfiguration.cs` | The remark used the word the source scan of AUTH-PASS-006 AC1 searches for, so the file describing the absence of the mechanism was read as the mechanism | AUTH-PASS-006 | The remark says what the table carries without using the forbidden term, and the scan stays as strict as the item states |

## 4. Decided in the owner's absence

Under D-161. Each entry is in `docs/reports/decisions-pending-review.md` in the same
words, numbered as it is there.

| # | Decision |
|---|---|
| 10 | The header the synchronizer token is presented in is `X-Janus-Csrf` |
| 11 | A password above the maximum is refused as a value above a ceiling |
| 12 | The offline and self-hosted corpora are files the deployment holds |
| 13 | A corpus whose age cannot be read is treated as no corpus |
| 14 | The application kind is declared at registration, and its zero value is the stricter one |
| 15 | Every layer of the browser profile answers the one code the chapter gives the layer |
| 16 | The framework's antiforgery is not used; the primitives under it are |
| 17 | AUTH-FACT-004's criteria are split across three phases |
| 18 | Nothing of the emitted-event surface is built in phase 3 |
| 19 | The configuration store's implementation waits for the runtime edits of phase 4 |
| 20 | INT-GEN-006 is built with the background jobs, and the session degrades to no location until then |
| 21 | Two source scans that could not be written as the criteria state them were rewritten to decide the same fact |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of the phase:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 244, `Janus.Authorization.Tests`
96, `Janus.Core.Tests` 384, `Janus.Hosting.Tests` 146, `Janus.Identity.Tests` 62,
`Janus.Privacy.Tests` 13 and `Janus.Storage.Tests` 188, none failing.

Full gate: GitHub Actions runs `35474527532` (push) and `35474534453` (pull request)
on branch `phase-03-sessions-2`, pull request #11, green on every job. `Integration
tests`, `Double migration run`, `Destructive-operation detection report`, `Truth-table
suite` and `Dependency vulnerability alerting` run on the pull-request event and
`Secret scanning` on the push event, as CONV-GATE-002 states, so the two runs together
are one pass of the table of CONV-GATE-001.

Pull request #10 carried the same tree on branch `phase-03-sessions`; nine commit
bodies ran over the 72-character bound of CONV-VCS-003, so the branch was replayed onto
`main` with those bodies corrected and the pull request replaced. The commit after the
two runs above changes this section alone.
