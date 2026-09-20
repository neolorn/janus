# Phase 6: Recovery, sign-in links, OIDC

Status: complete, full gate green, no open question. Admin-assisted re-enrolment with
its approvals, its break-glass approver and its enrolment session, public recovery and
the link that sets a password, loss reports with the notified invalidation window and
the credential redundancy it stands on, the recovery-code set and its export marks,
sign-in links and emailed codes on both channels with the same-browser press, the SMS
second step carried as a restricted entry, the new-device check and the policy grace
period, the preferred second step presented by the challenge, the OIDC provider with
its discovery document, key set, userinfo, authorization code flow with PKCE, silent
re-establishment, refresh rotation with family revocation and signing keys that rotate
with an overlap, and the return address a registration resolves from the client that
began it are built. The decisions taken in the owner's absence are in section 4 and, in
the same words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTH-RECOV-001 | AC1, AC2, AC3 | `CredentialServiceTests.AUTH_RECOV_001_AC1_ASyncedCredentialPromptsForNoSecondOneAsync`, `CredentialServiceTests.AUTH_RECOV_001_AC2_ADeviceBoundCredentialPromptsAndMayBeDeclinedAsync`, `CredentialServiceTests.AUTH_RECOV_001_AC3_AnEnforcedPolicyHoldsTheMemberToASecondOneAsync` |
| AUTH-RECOV-002 | AC1, AC2, AC3, AC4, AC5, AC6 | `RecoveryServiceTests.AUTH_RECOV_002_AC1_TheLinkExpiresAndIsNotReusedAsync`, `RecoveryServiceTests.AUTH_RECOV_002_AC2_TheReasonIsMandatoryAndRecordedAsync`, `RecoveryServiceTests.AUTH_RECOV_002_AC3_TheApproverCountIsConfigurableAsync`, `RecoveryServiceTests.AUTH_RECOV_002_AC4_AnUnusualApprovalFrequencyIsSurfacedAsync`, `RecoveryServiceTests.AUTH_RECOV_002_AC5_AnUnreachableMailboxOpensASessionThatMayReplaceItAsync`, `RecoveryServiceTests.AUTH_RECOV_002_AC6_TheLinkGoesToWhatTheAccountRecordsAsync` |
| AUTH-RECOV-002a | AC1, AC2, AC3 | `RecoveryServiceTests.AUTH_RECOV_002a_AC1_AnApproverCannotApproveTheirOwnRecoveryAsync`, `RecoveryServiceTests.AUTH_RECOV_002a_AC2_TheRefusalIsTheDomainsAndNotTheInterfacesAsync`, `RecoveryServiceTests.AUTH_RECOV_002a_AC3_ABreakGlassSessionApprovesTheAdministratorAsync` |
| AUTH-RECOV-003 | AC1, AC2 | `RecoveryServiceTests.AUTH_RECOV_003_AC1_AChannelTheRequesterSuppliedIsRefusedAsync`, `RecoveryServiceTests.AUTH_RECOV_003_AC2_TheChannelUsedIsRecordedAsync` |
| AUTH-RECOV-004 | AC1 | `RecoveryServiceTests.AUTH_RECOV_004_AC1_APolicyThatClosesRecoverySendsNothingAsync` |
| AUTH-RECOV-005 | AC1, AC2, AC3 | `RecoveryServiceTests.AUTH_RECOV_005_AC1_TheSecondFactorIsStillRequiredAfterRecoveryAsync`, `RecoveryFlowTests.AUTH_RECOV_005_AC2_TheLinkSetsAPasswordThatSignsInAsync`, `RecoveryServiceTests.AUTH_RECOV_005_AC2_RecoverySetsAPasswordAndRemovesNoFactorAsync`, `RecoveryServiceTests.AUTH_RECOV_005_AC3_TheRecoveredPasswordReportsTheLostPasskeyAsync` |
| AUTH-RECOV-006 | AC1, AC2, AC3, AC4 | `CredentialServiceTests.AUTH_RECOV_006_AC1_ASecondStepBesideAPasswordBringsRecoveryCodesAsync`, `RecoveryCodeServiceTests.AUTH_RECOV_006_AC2_EachWayOfTakingTheCodesAwaySetsTheExportAsync`, `CredentialServiceTests.AUTH_RECOV_006_AC3_RegeneratingReplacesTheWholeSetAsync`, `CredentialServiceTests.AUTH_RECOV_006_AC4_APasskeyOnlyAccountIsOfferedNoCodesAsync` |
| AUTH-RECOV-007 | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | `LossReportsTests.AUTH_RECOV_007_AC1_APasswordAloneReportsALossAndSuspendsItAtOnceAsync`, `RecoveryFlowTests.AUTH_RECOV_007_AC1_AReportedLossAnswersWithTheWindowAsync`, `LossReportsTests.AUTH_RECOV_007_AC2_ASuspendedCredentialIsRejectedAtSignInAsync`, `LossReportsTests.AUTH_RECOV_007_AC3_CancellingInsideTheWindowRestoresTheCredentialAsync`, `LossReportsTests.AUTH_RECOV_007_AC3_InvalidationIsHeldWhereNoNoticeDeliveredAsync`, `LossReportsTests.AUTH_RECOV_007_AC3_TheLinkInTheNoticeCancelsTheReportAsync`, `LossReportsTests.AUTH_RECOV_007_AC4_ReachableAssuranceMovesOnlyOnInvalidationAsync`, `CredentialServiceTests.AUTH_RECOV_007_AC5_RemovingOneOfSeveralCredentialsCompletesAtOnceAsync`, `CredentialServiceTests.AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync`, `LossReportsTests.AUTH_RECOV_007_AC6_TheLastSecondFactorTakesTheRecoveryCodesAsync`, `CredentialServiceTests.AUTH_RECOV_007_AC7_ARecoveredPasswordReportsThePasskeyAndEnrolsAnotherAsync` |
| AUTH-RECOV-007a | AC1, AC2 | `LossReportsTests.AUTH_RECOV_007a_AC1_APasswordThatMeetsTheFloorIsNotMarkedAsync`, `LossReportsTests.AUTH_RECOV_007a_AC1_AnInvalidationRereadsThePasswordAgainstTheFloorAsync`, `AuthenticationServiceTests.AUTH_RECOV_007a_AC2_ABelowFloorPasswordSignsInAndIsToldToChangeItAsync` |
| AUTH-RECOV-008 | AC1 | `LossReportsTests.AUTH_RECOV_008_AC1_SelfServiceLossReportingIsClosedToStaffAsync` |
| AUTH-FACT-002b | AC1, AC2, AC3, AC4, AC5, AC6 | `WebAuthnServiceTests.AUTH_FACT_002b_AC1_NeitherKindIsCreatedWithoutUserVerificationAsync`, `WebAuthnServiceTests.AUTH_FACT_002b_AC1_OnlyThePasskeyCeremonyIsDiscoverableAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC2_APasswordOnlyAccountIsAskedForNoSecondStepAsync`, `TotpServiceTests.AUTH_FACT_002b_AC2_ACodeGeneratorIsRefusedWithoutAPasswordAsync`, `WebAuthnServiceTests.AUTH_FACT_002b_AC2_ASecondStepIsRefusedWithoutAPasswordAsync`, `CredentialServiceTests.AUTH_FACT_002b_AC3_AFailedUpgradeChangesNothingAsync`, `CredentialServiceTests.AUTH_FACT_002b_AC3_AnUpgradedSecurityKeyIsListedAndTheEntryRetiresAsync`, `CredentialServiceTests.AUTH_FACT_002b_AC3_OnlyASecurityKeyIsUpgradedAsync`, `AuthenticationServiceTests.AUTH_FACT_002b_AC4_TheSecondStepChallengeOffersThePreferredMethodFirstAsync`, `FactorCatalogueTests.AUTH_FACT_002b_AC5_TheRestrictedEntriesAreTheOnesCarriedByText`, `SendingServiceTests.AUTH_FACT_002b_AC6_AnAbsentProviderIsItselfRecordedAsync`, `SendingServiceTests.AUTH_FACT_002b_AC6_NothingIsConsideredForAMessageThatIsNoFactorAsync`, `SendingServiceTests.AUTH_FACT_002b_AC6_TheSignalIsConsideredBeforeARestrictedFactorGoesAsync` |
| AUTH-FACT-003 | AC1, AC2, AC3, AC4, AC5 | `FactorCatalogueTests.AUTH_FACT_003_AC1_NoSignInOnlyEntryIsOfferedAsASecondStep`, `AssuranceTests.AUTH_FACT_003_AC2_AnEmailFactorSignsInAtAal1AndProvesNothingAfterwards`, `AuthenticationServiceTests.AUTH_FACT_003_AC2_ASignInLinkAloneRecordsAal1Async`, `AssuranceTests.AUTH_FACT_003_AC3_AnEmailFactorRaisesNothingBesideASecondFactor`, `AuthenticationServiceTests.AUTH_FACT_003_AC4_TheCodeTypedWhereTheSignInBeganCompletesItAsync`, `AuthenticationServiceTests.AUTH_FACT_003_AC4_TheLinkCompletesOnAPressInOneBrowserOnlyAsync`, `SignInFlowTests.AUTH_FACT_003_AC4_OnlyTheAskingBrowserIsSignedInByTheLinkAsync`, `AuthenticationServiceTests.AUTH_FACT_003_AC5_ALinkOnADisabledChannelSendsNothingAndSaysSoAsync` |
| AUTH-FACT-016 | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | `AuthenticationServiceTests.AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync`, `DeviceServiceTests.AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync`, `SignInFlowTests.AUTH_FACT_016_AC1_AHeldSignInLeavesTheBrowserWithNoSessionAsync`, `AuthenticationServiceTests.AUTH_FACT_016_AC2_TheEmailedCodeCompletesTheHeldSignInAsync`, `DeviceServiceTests.AUTH_FACT_016_AC2_ARememberedBrowserIsNotHeldAgainAsync`, `DeviceServiceTests.AUTH_FACT_016_AC2_TheRememberedPeriodIsWhatTheDeploymentConfiguresAsync`, `AuthenticationServiceTests.AUTH_FACT_016_AC3_WrongCodesInvalidateTheHeldSignInAsync`, `DeviceServiceTests.AUTH_FACT_016_AC3_ATwoFactorSignInIsNeverHeldAsync`, `SendingServiceTests.AUTH_FACT_016_AC4_TheCheckCodeCountsAgainstTheEmailDestinationAsync`, `DeviceServiceTests.AUTH_FACT_016_AC5_ThePassedCheckIsAnnouncedOnceAsync`, `DeviceServiceTests.AUTH_FACT_016_AC6_WithTheCheckOffNoSignInIsHeldAsync`, `RegistrationServiceTests.AUTH_FACT_016_AC7_TheRegisteringBrowserIsSeenForTheLifetimeAsync` |
| AUTH-FACT-017 | AC1, AC2, AC3, AC4 | `AuthenticationServiceTests.AUTH_FACT_017_AC1_WithNoGraceARaiseHoldsTheSignInAtEnrolmentAsync`, `PolicyGraceTests.AUTH_FACT_017_AC1_WithNoRunUpTheRequirementHoldsAtOnce`, `AuthenticationServiceTests.AUTH_FACT_017_AC2_InsideTheGraceTheSignInIsToldAndContinuesAsync`, `PolicyGraceTests.AUTH_FACT_017_AC2_AnAccountThatMeetsItIsHeldByNothing`, `PolicyGraceTests.AUTH_FACT_017_AC2_InsideTheRunUpTheSignInContinues`, `AuthenticationServiceTests.AUTH_FACT_017_AC3_AnAccountCreatedAfterTheChangeIsHeldAtOnceAsync`, `PolicyGraceTests.AUTH_FACT_017_AC3_AnAccountCreatedAfterTheChangeIsHeldAtOnce`, `AuthenticationServiceTests.AUTH_FACT_017_AC4_LoweringARequirementHoldsNothingAsync`, `PolicyGraceTests.AUTH_FACT_017_AC4_LoweringARequirementRaisesNothing` |
| AUTH-STEP-007 | AC1, AC2, AC3 | `CredentialServiceTests.AUTH_STEP_007_AC1_AnEnrolmentReachesEveryRecordedChannelAsync`, `CredentialServiceTests.AUTH_STEP_007_AC2_APasswordOnlyCustomerEnrolsAPasskeyAsync`, `CredentialServiceTests.AUTH_STEP_007_AC2_AnAccountReachingAal2MustPresentAal2Async`, `StepUpTests.AUTH_STEP_007_AC2_AnAccountReachingTwoFactorsPresentsTwo`, `StepUpTests.AUTH_STEP_007_AC2_EnrolmentIsGatedAtTheLowerOfTheTwo`, `RecoveryServiceTests.AUTH_STEP_007_AC3_TheRecoveredPasswordLeavesEveryOtherCredentialAsItWasAsync` |
| INT-SMS-001 | AC1, AC2, AC3, AC4 | `AuthenticationServiceTests.INT_SMS_001_AC1_NothingSentBySmsButTheSignInsOwnEntryIsAcceptedAsync`, `AuthenticationServiceTests.INT_SMS_001_AC2_WithTheSmsEntriesOffNothingIsSentToANumberAsync`, `RecoveryServiceTests.INT_SMS_001_AC3_TheEnrolmentLinkGoesByTextOnlyBehindAnApprovalAsync`, `SendingServiceTests.INT_SMS_001_AC4_ANoticeToAHeldPhoneAnswersToNotificationOnlyAsync` |
| REG-IDENT-003 | AC1, AC2 | `SignInFlowTests.REG_IDENT_003_AC1_ANonPrimaryVerifiedIdentifierOpensTheSameSignInAsync`, `SignInFlowTests.REG_IDENT_003_AC2_AnUnknownIdentifierAnswersAsAHeldOneDoesAsync` |
| REG-IDENT-007 | AC1, AC2, AC3 | `IdentifierServiceTests.REG_IDENT_007_AC1_TheSwapAppliesAndTheUndoGoesToTheOtherChannelAsync`, `IdentifierServiceTests.REG_IDENT_007_AC2_WithNoOtherChannelTheOldAddressConfirmsAsync`, `CredentialFlowTests.REG_IDENT_007_AC3_TheEnrolmentSessionReplacesTheLostAddressAsync`, `IdentifierServiceTests.REG_IDENT_007_AC3_AnEnrolmentSessionSwapsOnTheNewAddressAloneAsync` |
| REG-PM-001 | AC1, AC2 | `WebAuthnServiceTests.REG_PM_001_AC1_NoCeremonyCarriesAUserHandleAtAllAsync`, `WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync` |
| REG-SESS-008 | AC1, AC2 | `RegistrationServiceTests.REG_SESS_008_AC1_NoStepTakesADestination`, `RegistrationServiceTests.REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync` |
| IDN-ATTR-008 | AC1, AC2, AC3, AC4 | `AccountServiceTests.IDN_ATTR_008_AC1_TheLatestSecondStepIsPreferredWhileNothingIsMarkedAsync`, `AccountServiceTests.IDN_ATTR_008_AC2_AMethodTheAccountDoesNotHoldIsRefusedAsync`, `AccountServiceTests.IDN_ATTR_008_AC3_RemovingThePreferredOneMovesThePreferenceAsync`, `AuthenticationServiceTests.IDN_ATTR_008_AC4_TheChallengeFollowsThePreferenceAndOffersTheRestAsync` |
| AUTH-SESS-012 | AC1, AC2, AC3, AC4, AC5, AC6, AC7 | `OidcFlowTests.AUTH_SESS_012_AC1_ALiveSessionIsSentBackWithACodeAsync`, `OidcServiceTests.AUTH_SESS_012_AC2_ALiveSessionIssuesACodeWithoutInteractionAsync`, `OidcFlowTests.AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync`, `OidcFlowTests.AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync`, `OidcServiceTests.AUTH_SESS_012_AC3_ASilentRequestWithoutASessionIsRefusedAsync`, `OidcServiceTests.AUTH_SESS_012_AC4_ACodeExpiresWithinItsLifetimeAsync`, `OidcServiceTests.AUTH_SESS_012_AC4_ACodeIsSingleUseAsync`, `OidcServiceTests.AUTH_SESS_012_AC4_AnotherClientCannotExchangeTheCodeAsync`, `OidcServiceTests.AUTH_SESS_012_AC4_TheWrongVerifierExchangesNothingAsync`, `OidcFlowTests.AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync`, `OidcFlowTests.AUTH_SESS_012_AC6_TheExchangeHandsABrowserApplicationNoRefreshTokenAsync`, `OidcServiceTests.AUTH_SESS_012_AC7_TheExchangeNamesTheSessionRecordAsync`, `SessionServiceTests.AUTH_SESS_012_AC7_TheDerivedSessionStandsOnTheRecordAsync` |
| AUTH-OIDC-001 | AC1, AC2, AC3, AC4 | `OidcFlowTests.AUTH_OIDC_001_AC1_TheDocumentNamesTheEndpointsAndTheFlowsAsync`, `OidcFlowTests.AUTH_OIDC_001_AC2_AnUnregisteredClientIsForwardedNowhereAsync`, `OidcServiceTests.AUTH_OIDC_001_AC2_AWrongSecretObtainsNoTokenAsync`, `OidcServiceTests.AUTH_OIDC_001_AC2_AnUnregisteredClientObtainsNoCodeAsync`, `OidcStoreTests.AUTH_OIDC_001_AC2_TheRegistryHoldsWhatTheSecretHashesToAsync`, `OidcFlowTests.AUTH_OIDC_001_AC3_NoDynamicRegistrationEndpointExistsAsync`, `OidcFlowTests.AUTH_OIDC_001_AC4_TheProtocolClientsTokenIsTheSignedInPersonsAsync` |
| AUTH-OIDC-002 | AC1, AC2 | `OidcServiceTests.AUTH_OIDC_002_AC1_ABrowserApplicationIsIssuedNoRefreshTokenAsync`, `OidcServiceTests.AUTH_OIDC_002_AC2_AProtocolClientIsIssuedOneRefreshTokenAsync` |
| AUTH-OIDC-003 | AC1, AC2, AC3 | `OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync`, `OidcServiceTests.AUTH_OIDC_003_AC1_AReusedRefreshTokenRevokesTheFamilyAsync`, `OidcStoreTests.AUTH_OIDC_003_AC1_AFamilyIsRemovedWholeAsync`, `OidcServiceTests.AUTH_OIDC_003_AC2_TheRevocationIsAuditedAsync`, `OidcServiceTests.AUTH_OIDC_003_AC3_ARefreshTokenDoesNotOutliveTheRecordAsync` |
| AUTH-OIDC-004 | AC1, AC2, AC3 | `OidcServiceTests.AUTH_OIDC_004_AC1_NoTokenIsMintedFromARevokedRecordAsync`, `OidcServiceTests.AUTH_OIDC_004_AC2_TheAccessTokenLifetimeIsTheConfiguredOneAsync`, `OidcFlowTests.AUTH_OIDC_004_AC3_AnOfflineValidatorRefusesTheTokenAtItsExpiryAsync` |
| AUTH-KEY-001 | AC1, AC2, AC3, AC4 | `SigningKeysTests.AUTH_KEY_001_AC1_RotationNeedsNoRestartAndNoPersonAsync`, `SigningKeysTests.AUTH_KEY_001_AC2_ThePreviousKeyIsPublishedThroughTheOverlapAsync`, `SigningKeysTests.AUTH_KEY_001_AC3_ThePreviousKeyLeavesTheSetAfterTheOverlapAsync`, `OidcFlowTests.AUTH_KEY_001_AC4_TheKeySetCarriesTheConfiguredAlgorithmAsync`, `SigningKeysTests.AUTH_KEY_001_AC4_EveryPublishedKeyCarriesTheConfiguredAlgorithmAsync`, `SigningKeysTests.AUTH_KEY_001_AC4_ThePublishedSetCarriesNoPrivateMaterialAsync` |
| AUTH-KEY-002 | AC1, AC2 | `SecretMaterialTests.AUTH_KEY_002_AC1_NoConfigurationFileCarriesASecretValue`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedOnAFingerprintKeyShorterThanTheHash`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheFingerprintKey`, `KeyMaterialTests.AUTH_KEY_002_AC2_StartupFailsNamedWithoutTheKeyEncryptionKey` |
| AUTH-KEY-003 | AC1 | `OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheCodesThatHaveExpiredAsync`, `OidcStoreTests.AUTH_KEY_003_AC1_TheSweepTakesTheRefreshTokensThatHaveExpiredAsync`, `SessionStoreTests.AUTH_KEY_003_AC1_TheSweepTakesWhatHasPassedItsAbsoluteExpiryAsync`, `SigningKeysTests.AUTH_KEY_003_AC1_TheSweepRemovesWhatHasRetiredAsync` |
| API-CONV-001 | AC1, AC2 | `ApiConventionTests.API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync`, `OidcFlowTests.API_CONV_001_AC2_TheDocumentCarriesThePrefixTheHostMountedUnderAsync` |
| API-LAND-001 | AC1, AC2 | `SignInFlowTests.API_LAND_001_AC1_ALinkResolvesToDataAndNotToAPageAsync`, `SignInFlowTests.API_LAND_001_AC2_AnUnknownTokenYieldsACodeAndNoSessionAsync` |
| API-REDIR-001 | AC1, AC2, AC3, AC4 | `OidcFlowTests.API_REDIR_001_AC1_AnUnknownDestinationIsReplacedAndLoggedAsync`, `OidcServiceTests.API_REDIR_001_AC2_ADestinationContainingAKnownOneIsNotAcceptedAsync`, `RelyingPartyTests.API_REDIR_001_AC3_AnEntryThatIsNotAnAbsoluteOriginFails`, `OidcFlowTests.API_REDIR_001_AC4_OnlyTheReplacedDestinationIsRecordedAsync` |
| API-REDIR-002 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.API_REDIR_002_AC1_TheIdentifierIsResolvedWhereItIsCapturedAsync`, `RegistrationServiceTests.API_REDIR_002_AC2_AnUnrecognisedIdentifierIsTheDefaultAndNoRefusalAsync`, `RegistrationServiceTests.API_REDIR_002_AC3_NoLaterStepTakesADestination`, `RegistrationServiceTests.API_REDIR_002_AC4_TheReturnIsTheStoredClientsAndNoOthersAsync` |
| BFF-MACH-001 | AC1, AC2, AC3 | `BrowserProfileTests.BFF_MACH_001_AC1_NoBrowserEndpointCanBeMovedOntoTheMachineProfile`, `OidcFlowTests.BFF_MACH_001_AC2_ACookieOnAMachineRouteIsRefusedAsync`, `OidcFlowTests.BFF_MACH_001_AC3_TheTokenEndpointAuthenticatesTheClientAsync` |
| BFF-SESS-003 | AC1, AC2, AC3 | `BrowserCookieTests.BFF_SESS_003_AC1_NoCookieIsScopedToAParentDomain`, `OidcFlowTests.BFF_SESS_003_AC2_ASecondApplicationReEstablishesSilentlyAsync`, `BrowserProfileTests.BFF_SESS_003_AC3_EndingTheRecordTerminatesEveryApplicationsSessionAsync` |
| BFF-SESS-006 | AC1, AC2, AC3, AC5 | `OidcServiceTests.BFF_SESS_006_AC1_ALiveRecordIssuesTheCodeWithNobodyAskedAsync`, `OidcServiceTests.BFF_SESS_006_AC2_TheCodeAloneIsWhatTheBrowserCarriesAsync`, `OidcServiceTests.BFF_SESS_006_AC3_AReusedCodeAndAWrongVerifierAreRefusedAsync`, `OidcServiceTests.BFF_SESS_006_AC5_ARevokedRecordLeavesNothingStandingAsync` |
Criteria a test cannot decide, named here as CONV-TEST-007 requires:

| Criterion | How it was verified |
|---|---|
| API-LAND-001 AC3, that the landing route works with server-side rendering | The landing route is a page of the frontend, and the library produces no HTML at all: every link a user is sent resolves to a route that answers a code and a JSON body, which `SignInFlowTests.API_LAND_001_AC1_ALinkResolvesToDataAndNotToAPageAsync` decides. Nothing the library answers constrains how the frontend renders that route, so the criterion belongs to `18` and the applications of Milestone 2 step 10 |
| BFF-SESS-006 AC4, that after the exchange no token is persisted anywhere | The half the library owns is that the exchange hands a browser application nothing it could persist: no refresh token (`OidcServiceTests.AUTH_OIDC_002_AC1_ABrowserApplicationIsIssuedNoRefreshTokenAsync`, `OidcFlowTests.AUTH_SESS_012_AC6_TheExchangeHandsABrowserApplicationNoRefreshTokenAsync`) and an access token the exchange returns once and the library stores nowhere. What the back end for frontend does with the token it receives is that application's own code, which no phase of Milestone 1 builds (section 4, decision 66) |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| BFF-SESS-006 AC4 | The back end for frontend is the deployment's own layer; the library issues it nothing to persist, and what it holds afterwards is outside every project of this solution | Milestone 2 step 10; section 4, decision 66 |
| AUTH-FACT-002a AC5 | `POST /account/link/{provider}` needs a social consumer, which the library holds none of | `09` section 4; phase 5, decision 57 |
| REG-IDENT-008 AC1 to AC4 | A Google or Apple identity as a credential is `09` section 4, which no phase's chapter list names; phase 5 carried it here, and this phase's chapters are `09` sections 3, 5 and 9 | `09` section 4 |
| `POST /account/deactivate`, `POST /account/delete`, `POST /account/delete/cancel`, `POST /account/reactivate` | Deletion is the exercise of the erasure right, and the grace window ends in the anonymisation PRIV-RIGHT-005 governs | Phase 7 |
| The mail app password endpoints of `09` section 6 | The mailbox they belong to is provisioned against the mail server | Phase 8, INT-MAIL |
| `PUT /account/language` | Retired by D-146; `PUT /account/preferences` carries the language, and phase 5 built it | Nothing |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `.github/gates/acceptance-test-names.sh` | The job found an item's declaring chapter only where the bold marker is followed by a space, and several chapters follow it with a colon, so every criterion of those items read as undeclared | CONV-TEST-007, the working guide's section 3, a gate that mis-implements its own rule | The declaring line is the one that opens with the item, whether what follows the marker is a space or a colon, so the job checks what the chapter says and not how it is punctuated |
| `BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration` | The test asserted that the pipeline holds exactly one public mount method, and BFF-MACH-001 requires a second profile a host mounts | BFF-MACH-001 with BFF-OWN-001 AC1 | What AC1 states is that mounting takes no security-relevant configuration, so the test holds every public mount method to the one parameter, not the pipeline to one method |
| `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute` | The test read any source that names `Request.Path` as an endpoint excluded by configuration, and a machine profile cannot be selected without reading the path | BFF-MACH-001 AC1 with BFF-CSRF-001 AC2 | The path decides which profile carries a request and nothing else, so it is read in the one file that names the routes, `JanusPipeline.cs`, and the new `BFF_MACH_001_AC1` test holds the governed list to that file and to `MachineRoutes.cs` |
| `BrowserProfileTests.Mounted` | The harness mounted the browser profile without an authentication service, which the machine profile the same host now carries requires to resolve its scheme | the working guide's section 3, test infrastructure | The harness registers `AddAuthentication`, which lets the test exist and touches no runtime code and no shipped project's surface |
| `PasswordStoreTests.AUTH_PASS_003_AC1_TheTableCarriesNoExpiryFieldAsync` | The frozen column list predated the change-required mark AUTH-RECOV-007a AC2 stores | AUTH-RECOV-007a AC2 | The list is the table as this phase leaves it, so `change_required` joins it |
| `SessionStoreTests.AUTH_KEY_003_AC1_TheSweepTakesWhatHasPassedItsAbsoluteExpiryAsync` | The sweep counted the rows the other tests of the class had left in the shared database as well as its own | AUTH-KEY-003 AC1 | The test sweeps once before it begins, so what the counted sweep takes is its own expired session and nothing else |
| `ErrorCodesTests.Catalogue`, `VocabularyContractTests` | The frozen catalogue and the frozen wire-name list predated this phase's codes and message kind | `10` section 1 and `10` section 5.19, which name them | The lists are the vocabulary as this phase leaves it, so `auth.credential.notupgradable`, `model.startup.kekunavailable` and `recovery-link` join them |
| `ICredentials.RemoveAsync`, `IOidc.FindClientAsync` | Both reported absence by returning a null inside a success, and no contract returns a null from a lookup | CONV-DESIGN-005 AC1 and AC2 | Each reports the absence as the outcome the chapter names: the removal fails with `auth.credential.lastsecondfactor` carrying `invalidatesAt`, which `09` gives a 202, and the lookup fails with `authz.denied` |

## 4. Decided in the owner's absence

Under D-161. Each entry is in `docs/reports/decisions-pending-review.md` in the same
words, numbered as it is there.

| # | Decision |
|---|---|
| 58 | The provider runs on OpenIddict in degraded mode |
| 59 | The server's own keys are ephemeral and protect nothing |
| 60 | What API-REDIR-001's configured list is at each endpoint |
| 61 | Where a browser holding no session is sent |
| 62 | Which routes the machine profile governs |
| 63 | A deployment that starts without key material refuses to start |
| 64 | Sessions past their absolute expiry are swept |
| 65 | The integration container's credential is drawn per run |
| 66 | The browser half of BFF-SESS-006 is the deployment's |
| 67 | The restricted channel is a catalogue property |
| 68 | What is known about a number is considered and recorded, not refused on |
| 69 | The upgrade refuses with a code of its own |
| 70 | Nothing carries "shown" or "exported" to the library |
| 71 | The other half of AUTH-OIDC-001 AC4 belongs to later phases |
| 72 | BFF-MACH-001's break-glass and provider callbacks reach past this phase |
| 73 | Two contracts reported absence with a null |
| 74 | API-REDIR-002 is built in the phase that first can |
| 75 | A user handle is proved absent rather than made safe |
| 76 | A prefix moves the provider's endpoints with the rest |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of the phase:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 531, `Janus.Authorization.Tests`
96, `Janus.Core.Tests` 414, `Janus.Hosting.Tests` 243, `Janus.Identity.Tests` 62,
`Janus.Privacy.Tests` 13 and `Janus.Storage.Tests` 206, none failing.

Full gate: GitHub Actions runs `35505368896` (push) and `35505370367` (pull request) on
branch `phase-06-signin`, pull request #14, green on every job. `Integration tests`,
`Double migration run`, `Destructive-operation detection report`, `Truth-table suite`
and `Dependency vulnerability alerting` run on the pull-request event and `Secret
scanning` on the push event, as CONV-GATE-002 states, so the two runs together are one
pass of the table of CONV-GATE-001.

The commit after the two runs above changes this section alone.
