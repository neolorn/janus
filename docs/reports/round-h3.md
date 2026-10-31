# Round H3: what a host's own frontends meet

## 1. Items implemented

Items are D-224's, by number, as D-224 now reads (23 items), then D-225's and
D-226's. Criteria proven by tests that existed before the round, and still pass, are
marked "at base".

- Item 1, the staged registration's arrays (REG-SESS-001):
  - `RegistrationSessionStoreTests.REG_SESS_001_ASessionWithEveryOptionalArrayNullReadsBackWithEachNullAsync`;
  - `RegistrationSessionStoreTests.REG_SESS_001_ASessionWithEveryArraySetReadsBackAsItWasWrittenAsync`;
  - `RegistrationSessionStoreTests.REG_SESS_001_AnEmptyArrayReadsBackEmptyAndWritesNoLinkRowAsync`.
- Item 2, a sign-in that proved the mailbox (AUTH-FACT-016 AC8):
  - `AuthenticationServiceTests.AUTH_FACT_016_AC8_ALinkToThePrimaryEmailCompletesWithoutTheCheckAsync`;
  - `AuthenticationServiceTests.AUTH_FACT_016_AC8_AnEmailCodeToThePrimaryEmailCompletesWithoutTheCheckAsync`;
  - `AuthenticationServiceTests.AUTH_FACT_016_AC8_TheCodeALinksPageShowedCompletesWithoutTheCheckAsync`;
  - `AuthenticationServiceTests.AUTH_FACT_016_AC8_AnEmailCodeToAnotherEmailIsHeldWithItsCodeToThePrimaryAsync`;
  - `AuthenticationServiceTests.AUTH_FACT_016_AC8_APhoneLinkIsHeldWithItsCodeToThePrimaryAsync`;
  - the sample host: `FrontendTests.AUTH_FACT_016_AC8_ALinkPressedInANewBrowserSignsInWithNoSecondMailAsync`.
  - AC9 (the resend after a restriction) is not built, with item 18 (section 2).
- Item 3, the registration stream's first bytes (FE-VER-001, REG-SESS-003):
  `RegistrationWizardTests.FE_VER_001_TheStreamSpeaksAtOnceAndAfterThirtySecondsOfSilenceAsync`.
- Item 4, parallel first requests: by design (BFF-CSRF-005a as D-224 amends it); no code.
  Verified by reading: two requests that carry no cookie are each issued a
  pre-authentication session, and the later answer's cookies replace the earlier's.
- Item 5, `/recovery/complete` declares the password refusals (AUTH-RECOV-005):
  `RecoveryFlowTests.AUTH_RECOV_005_APasswordTheScreeningRefusesIsAnsweredWithItsRefusalAsync`.
- Item 6, whether a password is set (REG-ACCT-001, FE-SEC-001):
  - `AccountServiceTests.REG_ACCT_001_TheReadSaysWhetherAPasswordIsSetAndWhenAsync`;
  - `AccountApplicationTests.FE_SEC_001_TheAccountSaysWhetherAPasswordIsSetAsync`;
  - the sample host: `FrontendTests.FE_SEC_001_AnEnrolmentSessionReadsItsScreensDocumentAsync`.
- Item 7, when `trustDeviceOffered` holds (AUTH-FACT-015 AC9):
  - `AuthenticationServiceTests.AUTH_FACT_015_AC9_TheSecondStepsAnswerTellsWhetherItsCompletionMayTrustTheBrowserAsync`;
  - `AuthenticationServiceTests.AUTH_FACT_015_AC9_TheSecondStepsAnswerOffersNoTrustItsCompletionCouldNotRecordAsync`.
- Item 8, the legal texts are CommonMark (PRIV-CONS-005): no code. Verified by reading: the
  library stores and serves a version's text as given and renders none.
- Item 9, an acknowledgement downgrades (AUTH-SESS-009, FE-API-004):
  `InvitationAcknowledgementFlowTests.AUTH_SESS_009_TheAcknowledgementEndsNoSessionAndTheNextGatedRequestStepsUpAsync`.
- Item 10, a startup failure names itself (CONV-ERR-001 AC4):
  - `StartupExceptionTests.CONV_ERR_001_AC4_TheMessageHoldsTheCodeAndEveryDetailsNameAndValue`;
  - `StartupExceptionTests.CONV_ERR_001_AC4_AFailureWithNoDetailsAddsItsCodeAlone`;
  - `StartupExceptionTests.CONV_ERR_001_AFaultWithoutAFailureKeepsItsSentence`.
- Item 11, the provider answers at its own address (AUTH-OIDC-001, LIB-HOST-003, `09`
  section 9):
  - `OidcFlowTests.AUTH_OIDC_001_TheIssuerIsTheProviderAddressAndEachRouteIsUnderItAsync`;
  - `OidcFlowTests.AUTH_OIDC_001_AProviderRouteAtAnotherAddressIsNotTheProvidersAsync`
    (a theory over the six routes).
- Item 14, an enrolment session's password and screen (FE-SEC-001, AUTH-RECOV-002,
  FE-PM-002):
  - `CredentialServiceTests.FE_SEC_001_APasswordThePolicyDoesNotAdmitIsRefusedOnEverySessionAsync`;
  - `CredentialFlowTests.FE_SEC_001_APasswordThePolicyDoesNotAdmitIsRefusedOnTheEnrolmentSessionAsync`;
  - `CredentialFlowTests.FE_SEC_001_TheEnrolmentSessionsOpeningCarriesTheDocumentItsReadAnswersAsync`;
  - `CredentialFlowTests.AUTH_RECOV_002_TheEnrolmentDocumentNamesTheEmailsWhereTheMailboxWasLostAsync`;
  - the sample host: `FrontendTests.FE_SEC_001_AnEnrolmentSessionReadsItsScreensDocumentAsync`.
- Item 15, what the frontend may know of the settings (REG-IDENT-001, REG-IDENT-002,
  REG-PROF-001, AUTH-PASS-001, AUTH-PRIN-002, PRIV-BASIS-003, REG-INV-001):
  - `FrontendSettingsTests.REG_IDENT_001_ABrowserCarryingNothingReadsTheKeysAndTheSystemPolicyAsync`;
  - `FrontendSettingsTests.AUTH_PRIN_002_TheSettingsCarryThePolicyInForceForTheSignedInAccountAsync`;
  - `FrontendSettingsTests.PRIV_BASIS_003_ThePurposesAPersonDecidesAreAnsweredWithWhatEachTakesAsync`;
  - `FrontendSettingsTests.REG_INV_001_ARegistrationAtAnInvitationReadsThePolicyOfTheOrganizationItJoinsAsync`;
  - the sample host: `FrontendTests.REG_IDENT_001_ABrowserWithNoSessionReadsTheSettingsAsync`.
- Item 16, registration steps 7 to 10: held by the frontend (FE-REG-005); no code.
  Verified by reading: the server state of a registration session is unchanged.
- Item 19, a link-borne call's `{id}` (FE-VER-001 AC8), the loss-report cancel under
  D-226 item 2 below:
  - `RegistrationWizardTests.FE_VER_001_AC8_ARegistrationLinkPressedUnderTheNilIdentifierVerifiesAsync`;
  - `RegistrationWizardTests.FE_VER_001_AC8_AnIdentifierLinkUnderTheNilIdentifierActsOnWhatItsTokenNamesAsync`;
  - `AccountApplicationTests.FE_VER_001_AC8_AnUndoUnderTheNilIdentifierRestoresWhatItsTokenNamesAsync`.
- Item 20, the username on a reset form (FE-PM-002):
  - `RecoveryFlowTests.FE_PM_002_TheResetFormReadsItsUsernameFromTheTokenAndSpendsNothingAsync`;
  - `RecoveryFlowTests.FE_PM_002_AUsernameReadOfATokenPastItsLifetimeOrSpentIsRefusedAsync`;
  - `RecoveryFlowTests.FE_PM_002_AUsernameReadOfATokenThatOpensNothingIsCountedThenThrottledAsync`.
- Item 21, recovery codes saved at registration (FE-SEC-001, AUTH-FACT-008):
  `RegistrationFlowTests.FE_SEC_001_TheExportOfARegistrationsSetIsReportedOnTheSessionTheTermsStepEstablishesAsync`.
  No code change was needed.
- Item 22, `landing` on every session of the record (REG-SESS-008 AC3):
  `SignOnTests.REG_SESS_008_AC3_ASessionSignedOnFromTheRecordAnswersItsLandingAsync`.
- Item 23, the adapter meets a real Stalwart (INT-MAIL-001, CONV-TEST-002,
  CONV-DESIGN-008), against Stalwart 1.0.1 pinned by digest
  (`stalwartlabs/stalwart@sha256:b9da4df8b4efc06bc926dd6e990bdeb0ad7b9261a7c3893d74b0461a00bb389b`),
  configured without its setup wizard, through Testcontainers 4.15.0; INT-MAIL-010's calls
  under D-226 item 1 below; INT-MAIL-004 criteria 1 to 3 at the server are not built
  (section 2):
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC3_EveryProvisioningOperationIsAJmapRequestTheServerAppliesAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC4_AReplayedPushConvergesOnTheAccountItCreatedAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC4_AnAccountNotCarryingTheIdentifierIsNeverAdoptedAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC5_AnEnabledPushToAReplaceAccountEnablesAuthenticationAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC6_ADisabledPushChangesAuthenticationAloneAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_ARemovalDestroysTheMailboxsOwnAccountAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_TheListingAnswersEachAccountAsTheServerHoldsItAsync`;
  - `JmapMailServerAgainstStalwartTests.CONV_TEST_002_AC5_TheServerDecidingTheCriteriaIsItsReleaseNamedByDigest`;
  - `JmapMailServerAgainstStalwartTests.CONV_TEST_002_TheServerIsReachedOverTlsTrustingItsCertificateAloneAsync`;
  - `LibraryStructureTests.ContainerPackage_InThePackageVersions_StandsAtTheVersionThePostgreSqlPackageBrings`.
- `10` section 4.9 `oidc.continuation.lifetime`: declared as its row states (default
  30 minutes, R, ceiling 1 hour), proven by the reference gate
  (`SettingsCatalogueTests.REF_001_AC1_EveryLiveRowOfTheReferenceIsAKeyInTheSource`). No
  code reads it while item 12 is not built (section 2).
- The sample host (LIB-TEST-004), of the plan's row: an email-link sign-in from a new
  browser with no second mail; the settings, the enrolment document and whether a
  password is set (the three `FrontendTests` above). The sign-on and back to the
  application's route and the restore in place wait with items 12 and 13 (section 2).


D-225, by item:

- Item 1, messages leave through the host (INT-SMS-006, INT-GEN-005, INT-MAIL-008,
  IDN-ATTR-001, LIB-EXT-001, LIB-TEST-003, PRIV-ROPA-002):
  - `IntegrationBoundaryTests.INT_SMS_006_AC1_NoGatewayOrSenderIsCalledByTheLibrary`;
  - `SendingGovernanceTests.INT_SMS_006_AC3_ARetryHandsTheSameMessageOverUnderItsReferenceAsync`;
  - `SendingGovernanceTests.INT_GEN_005_AC1_AMailHandedOverCarriesItsExactFieldSetAsync`;
  - `SendingGovernanceTests.INT_GEN_005_AC1_ATextHandedOverCarriesItsExactFieldSetAsync`;
  - `SendingGovernanceTests.D_022_AHandlerRefusalLeavesTheMessageRecordedAsync`;
  - `SendingGovernanceTests.AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeTheHandlerIsCalledAsync`
    and `ThrottlingTests.AUTH_ABUSE_003_AC7_AnAskIsAnsweredBeforeTheHandlerIsCalledAsync`;
  - `IDN_ATTR_001_AMessageInEveryLanguageReadsBackCarryingThemAllAsync`;
  - `MailboxPublisherTests.INT_MAIL_001_MailboxHostingIsARegistrationOfItsOwnAsync` and
    `MailboxPublisherTests.INT_MAIL_001_AnotherMailServerIsARegistrationAndNoCodeChangeAsync`;
  - `LIB_TEST_003_TheHandlerKeepsEveryMessageByDestinationAndKindAsync`,
    `LIB_TEST_003_EachVerificationLinkTheHandlerHoldsIsPressedAsync` and
    `LIB_TEST_003_TheHandlerFailsAHandOverOnlyWhereTheTestSaysSoAsync`;
  - `PRIV_ROPA_002_TheShippedRegisterIsTheThreeRowsTheLibraryMakesTrue`;
  - INT-SMS-003's criteria as D-225 renumbers them: the `INT_SMS_003_AC1_` tests.
- Item 2, outcomes come from the host (INT-SMS-005, AUTH-ABUSE-004):
  - `INT_SMS_005_AC1_AReportOfDeliveryMarksNothingAndReleasesNothingAsync`;
  - `INT_SMS_005_AC2_ADeliveredSecondFactorTextReleasesOnlyItsResendCountAsync`;
  - `INT_SMS_005_AC2_AFailureReportReleasesEveryCountAsync`;
  - `INT_SMS_005_AC3_ARepeatedReportChangesNothingAsync`;
  - `INT_SMS_005_AC3_AReportNamingNoMessageChangesNothingAsync`;
  - `OutcomeRouteTests.INT_SMS_005_AC4_NoRouteOfTheLibraryTakesAGatewaysReport`;
  - `SendingGovernanceTests.AUTH_ABUSE_004_AC2_AFailureReportReleasesEveryCountOfATextInEveryLanguageAsync`;
  - `CONV_DESIGN_003_AC8_AReportInsideAnOpenUnitOfWorkIsAFaultAsync`.
- Item 3, alerts raised here and delivered by the host (OPS-ALERT-001 to OPS-ALERT-003,
  OPS-BOOT-002, OPS-BOOT-004):
  - `OPS_ALERT_001_AC3_OnlyAConditionWhoseTransactionCommitsReachesTheHandlerAsync`;
  - `OPS_ALERT_001_TheValuesRunInTheOrderOfTheTable`;
  - `OPS_ALERT_002_AC1_OneConditionInsideTheWindowIsHandedOverOnceAsync`;
  - `OPS_ALERT_002_AC3_TwoScopesOfOneConditionAreBothHandedOverAsync`;
  - `OPS_ALERT_002_NoHandlerIsCalledWhileATransactionIsOpenAsync`;
  - `OPS_ALERT_003_AC2_ARaisedAlertIsHandedToTheHandlerOnceAsync`;
  - `OPS_ALERT_003_AC2_AnAlertTheHandlerDidNotTakeIsHandedAgainAsync`;
  - `OPS_ALERT_003_AC3_TheLapseOfAStalledCarrierReachesTheHandlerAsync`;
  - `OPS_BOOT_002_AC3_UseIsHandedToTheAlertHandlerAsync`;
  - `OPS_BOOT_004_AC2_GenerationIsAuditedAndAlertedAsync`.
- Item 4, the server is the deployment's to watch: the watches, their seams, conditions
  and scopes are removed with their tests and rows, and no criterion remains to test.
  Verified by the vocabulary and reference gates.
- Item 5, backups are the deployment's (OPS-SEC-003, DR-006a, DR-009a):
  - `OPS_SEC_003_AC4_NoRunPrintsAKeyAndTheRotationRetiresOnlyWhenAskedAsync`;
  - `OPS_SEC_003_AC4_ARetirementBeforeTheRotationCompletesIsRefusedAsync`;
  - `OPS_SEC_003_AC4_TwoRetirementsAtOnceRetireTheRotationOnceAsync`;
  - `OPS_SEC_003_AC4_AnArgumentTheCommandDoesNotTakeIsRefusedAsync`;
  - `OPS_SEC_003_AC6_NoRunPrintsAKeyAndTheRotationRetiresOnlyWhenAskedAsync`;
  - `OPS_SEC_003_AC6_ARetirementBeforeTheRotationCompletesIsRefusedAsync`.
- Item 6, no licence register and no maintenance log; the cryptoperiod's warning runs as
  `kek-cryptoperiod` (DR-009a):
  - `DR_009a_AC1_TheCryptoperiodInsideTheLeadIsRaisedAsync`;
  - `DR_009a_AC1_ARotationWithinItsCryptoperiodRaisesNothingAsync`;
  - `DR_009a_AC1_ACompletedRotationEndsTheWarningAsync`;
  - `DR_009a_AC1_ADeploymentNeverRotatedCountsFromBootstrapAsync`;
  - `DR_009a_AC1_TheCryptoperiodIsReadFromTheKeysOwnRotationRecordAsync`;
  - `DR_009a_AC1_TheLeadIsTheConfiguredOneAsync`;
  - `DR_009a_AC6_AFingerprintKeyRotationDoesNotEndTheWarningAsync`.
- Item 7, the procedures that leave the specification (OPS-MIG-001, PRIV-BREACH-001):
  - `OPS_MIG_001_AC5_WhatTheValuesNameIsDestructive`;
  - `PRIV_BREACH_001_AC3_NoProcedureDefersTheClockPendingAssessment`, the checks of the
    retired document criteria removed.
- Item 8, a frontend's technology is its own: no code.
- Item 9, what was out of date: no code. LIB-PKG-003 attaches the packages to the tagged
  release, which the release workflow of the plan's section 4 does, and a package feed
  is a host's; the Actions minutes and their review were never code; the privacy
  defaults are unchanged.
- Item 10, what a host supplies (LIB-HOST-001, LIB-TEST-003):
  - `StartupValidationTests.INT_SMS_006_AC2_ADeploymentWithNoNotificationHandlerDoesNotStartAsync`;
  - `SendingValidationTests.INT_SMS_006_AC2_ADeploymentWithNoNotificationHandlerIsRefusedByTheSendingCheckAsync`;
  - `OPS_ALERT_003_AC4_ADeploymentWithNoAlertHandlerDoesNotStartAsync`;
  - `SendingValidationTests.OPS_ALERT_003_AC4_ADeploymentWithNoAlertHandlerIsRefusedByTheSendingCheckAsync`;
  - `Janus.Testing`'s in-memory handlers, with the `LIB_TEST_003_` tests above.

D-226, by item:

- Item 1, the mail server's tokens (INT-MAIL-001, INT-MAIL-010, REG-MAIL-002,
  AUTH-OIDC-001, AUTH-OIDC-006, LIB-HOST-001), the calls read against Stalwart's own
  documentation (an app password for another user; the OIDC directory):
  - `AppPasswordsTests.INT_MAIL_010_AC1_TheCallsActOnTheAccountOfTheMailboxHeldAndNoOtherAsync`;
  - `AppPasswordFlowTests.INT_MAIL_010_AC1_TheServerIsCalledOnTheAccountOfTheMailboxHeldAsync`;
  - `JmapMailServerTests.INT_MAIL_010_AC1_AnAppPasswordIsOneCallOnTheMailboxsAccountAsync`;
  - `JmapMailServerTests.INT_MAIL_010_ACallActsOnNoAccountButTheOneCarryingTheMailboxAsync`;
  - INT-MAIL-001 AC7 for INT-MAIL-010's calls, at the pinned server:
    `JmapMailServerAgainstStalwartTests.INT_MAIL_010_AC1_AnAppPasswordIsManagedOnTheAccountOfTheMailboxHeldAsync`
    (created, listed and revoked with the management key on the request-level
    `accountId`; the secret signs a mail client in until it is revoked and not after,
    AC2 and AC3) and
    `JmapMailServerAgainstStalwartTests.INT_MAIL_010_ACallActsOnNoAccountButTheOneCarryingTheMailboxAsync`;
    `CONV_TEST_002_AC5_TheServerDecidingTheCriteriaIsItsReleaseNamedByDigest` now
    requires INT-MAIL-010's test there too;
  - no token for the mail server, no `MailServerClient` and no startup check of one
    (AUTH-OIDC-001 AC4):
    `StartupValidationTests.AUTH_OIDC_001_AC4_ADeploymentHostingMailStartsWithNoClientDeclaredForItAsync`;
  - a client receives the scopes it is registered with (AUTH-OIDC-001 AC5):
    `OidcFlowTests.AUTH_OIDC_001_AC5_AClientReceivesAScopeItIsRegisteredWithAsync` and
    `OidcFlowTests.AUTH_OIDC_001_AC5_AScopeTheClientIsNotRegisteredWithIsRefusedAsync`;
    the four scopes as Q11 says;
  - AUTH-OIDC-006 AC3, a verifier configured as the mail server is:
    `ProviderConformanceTests.AUTH_OIDC_006_AC3_TheAdapterRefusesATokenForAnotherAudienceAsync`,
    at base.
- Item 2, a loss report's link (AUTH-RECOV-007, FE-VER-001 AC8, D-224 item 19):
  - `LossReportsTests.FE_VER_001_AC8_TheLinkCancelsTheReportItsTokenNamesWhateverTheCallNamesAsync`;
  - `LossReportsTests.AUTH_RECOV_007_ATokenThatFindsNoReportCancelsNothingAsync`;
  - `RecoveryFlowTests.FE_VER_001_AC8_ALossReportsLinkUnderTheNilIdentifierCancelsItsReportAsync`;
  - the column, not null and unique, and the migration
    `20261031044442_FindALossReportByItsCancelToken`, which gives each row present a
    random value: `ModelTests` and `schema.txt`, and the migrations run twice in the
    pipeline. No test reads a report back by its fingerprint from the real store.

Verified by a run on this machine, besides the fast checks: the three
`RegistrationSessionStoreTests` above (integration, run once while written); the
`JmapMailServerAgainstStalwartTests` (run while written, as the round's instruction
allows); the sample host's solution packed, restored, built and checked by
`dotnet format --verify-no-changes`, its tests not run. Every other test of the round
runs first in the pipeline.

## 2. Items not implemented

Not built in this turn, for want of time; nothing of them is in the code:

- D-226 item 3, a challenge's browser, with D-224 items 17 and 18 and item 2's resend
  clause (AUTH-FACT-016 AC9).
- D-226 item 4 with D-224 item 12, back to the application after sign-in
  (AUTH-SESS-012 AC10, `continueTo`, `GET /oidc/authorize/continue`).
- D-226 item 5 with D-224 item 13, restoring a session in place
  (`POST /auth/restore/begin`, `POST /auth/restore`, BFF-STEP-001, BFF-ORDER-001
  stage 5).
- INT-MAIL-004 criteria 1 to 3 against the real server (criterion 4): Stalwart
  configured as INT-MAIL-004 states against the provider of a test host listening on a
  real port.
- The sample host's sign-on and back and restore in place, with items 12 and 13.

## 3. Resolved by rule

- `tests/Janus.Hosting.Tests/Deployment.cs` (test infrastructure):
  - What was out of step: a deployment mounted under a prefix declared its provider
    address without the prefix, so no prefixed test could reach the provider once it
    answers at its declared address alone.
  - Governing item: LIB-HOST-003, which names the provider address as the mount, prefix
    included.
  - Rule: the fixture declares the provider address at its mount, prefix included.
- `tests/Janus.Hosting.Tests/Oidc/Machine.cs` (test infrastructure):
  - What was out of step: the machine client could not address a host other than the
    provider's, which `AUTH_OIDC_001_AProviderRouteAtAnotherAddressIsNotTheProvidersAsync`
    needs.
  - Governing item: `09` section 9 (D-224), a provider route at another address is not the
    provider's.
  - Rule: the client takes the host it sends to, defaulting to the provider's.
- `DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis` (a gate):
  - What was out of step: its list of the files carrying a basis key as a value lacked
    `PurposeSettingView.cs`, which writes `consent` as the wire name of what a purpose
    takes in `GET /settings` and branches on no basis.
  - Governing item: PRIV-BASIS-001 AC4, no library source chooses a path by a basis; `09`
    `GET /settings`, `takes` is `consent` or `objection`.
  - Rule: the gate lists the file beside the other two wire names that are no basis, the
    value asserted by
    `FrontendSettingsTests.PRIV_BASIS_003_ThePurposesAPersonDecidesAreAnsweredWithWhatEachTakesAsync`,
    and its branching check is unchanged.
- `IdentityEndpointsTests.LIB_API_005_AC3_EveryEndpointResolvesExactlyOneServiceOfTheContractAsync`
  (a gate):
  - What was out of step: its list of the routes no operation maps lacked `GET /settings`.
  - Governing item: `07`, `GET /settings` reads the deployment's settings and no record and
    is not an operation (D-224).
  - Rule: the gate lists the route beside the other routes that are not operations.
- `tests/Janus.Hosting.Tests/Authorization/ServedRequests.cs` (test infrastructure):
  - What was out of step: the harness starts the provider without the startup check and
    declared no provider address, which the provider's routes are built from since
    4a388b52.
  - Governing item: LIB-HOST-001 and LIB-HOST-003, the host declares where its provider
    answers.
  - Rule: the harness declares its addresses as `tests/Janus.Hosting.Tests/Deployment.cs`
    does.
- `AlertCondition` (`src/Janus.Core`):
  - What was out of step: once D-225 removed two conditions, the members' values no
    longer ran in the order of `10`'s table.
  - Governing item: OPS-ALERT-001, the conditions as `10` lists them.
  - Rule: the values are renumbered in table order, as
    `OPS_ALERT_001_TheValuesRunInTheOrderOfTheTable` asserts.


## 4. Open questions

**Q1 (Tier 3), answered by D-226 item 1.**
Stalwart 1.0.1 refuses the library's access tokens under INT-MAIL-004's
configuration.

- Server: Stalwart v1.0.1 (2026-10-28), the newest 1.0 patch, pinned by the digest in
  section 1, configured without the wizard through recovery mode and JMAP, with an `Oidc`
  directory naming a test issuer, the mail-server client as accepted audience, no
  introspection.
- What it answered to `x:AppPassword/get` with a person's token issued to its client:
  HTTP 401, `{"type":"about:blank","status":401,"title":"Unauthorized","detail":"You have
  to authenticate first."}`; its log: the token carries none of the
  `urn:ietf:params:oauth:scope:{mail,contacts,calendars,files}` scopes and
  `unscopedTokens` is set to reject.
- Against the contract: INT-MAIL-004 AC1 needs the token accepted; INT-MAIL-010 lists,
  creates and revokes app passwords with a person's token. The 1.0 documentation places
  `x:AppPassword/*` under `urn:stalwart:jmap`, which it grants to unscoped tokens only.
  `requireAudience`, which INT-MAIL-004 names, does not exist in 1.0; the server has
  `acceptedAudiences` and `skipAudienceValidation`, and accepts an `aud` matching an
  accepted audience or a resource identifier of its own.
- As the contract states: a token issued to another client is refused, HTTP 401 (AC3).
- Recorded only: `unscopedTokens` `interoperable` answers 200 with a method error
  `forbidden`; `full` succeeds, and the server's own description says it grants
  management permissions to any such token.
- Parks: INT-MAIL-004 AC1 and AC4 at the real server, INT-MAIL-010's calls there, and
  the test host on a real port they need. The adapter and its contract are unchanged.

**Q2 (Tier 2), answered by D-226 item 2.**
The loss-report cancel found by its token (item 19).

- What the code needs: the loss report a cancel link's token names, found from the token
  alone under the nil identifier.
- What the spec says: D-224 item 19, the token names what the call acts on.
- What the code holds: `loss_reports` is keyed by `credential` and keeps the token
  encrypted in `enc_cancel`, with no fingerprint to find it by. A fingerprint column needs
  a migration, which cannot fill the rows already open.
- Reading A: the column NOT NULL; reports open at the migration are cancelled from a
  session only. Smallest fix: a sentence in `09`.
- Reading B: the column nullable, filled at the report's next notice. Smallest fix: a
  sentence in `09`.
- Reading C: reports open at the migration keep being read by `{id}`. Smallest fix: a
  sentence in `09`.
- Parks: the loss-report half of item 19, and its share of FE-VER-001 AC8.

**Q3 (Tier 3), answered by D-226 item 3.**
A challenge's binding to the pre-authentication cookie (items 17, 18).

- Items 17 and 18 find a challenge through the pre-authentication cookie it is bound to.
  No challenge is bound to one: `POST /auth/begin` opens a challenge bound to nothing, and
  `POST /auth/link` `{ identifier }` names no challenge, so a first-step link carries none.
- Parks: items 17 and 18, and item 2's clause on item 18's resend (AUTH-FACT-016 AC9).

**Q4 (Tier 3), void by D-225: its seam and route are removed.**
`ISmsReports.TakeAsync` and its access context (D-224's former item 25).

- LIB-API-001 lists `ISmsReports` among the operations, called in process only.
- `07` says every operation takes an access context and is authorized by the gate; its
  paragraph on in-process operations names four, none with an endpoint or a gate, and
  `ISmsReports` is not among them. `10` section 2.1 holds no permission that could gate it.
- Parks: item 25 (INT-SMS-005 AC5) and the sample host's report route with its test.

**Q5 (Tier 3), void by D-225: its route is removed.**
The reports' key in the route's path, and the application's own logs (D-224's former
item 24).

- INF-TLS-004 and D-224 keep the key out of the reverse proxy's access log only. Inside
  the application, ASP.NET Core writes the request path: routing at Debug, "Request
  starting" at Information, and a host's `HttpLogging` `RequestPath`. CONV-LOG-003 says a
  secret is never logged; whether the library keeps the key out of the framework's logs,
  and how, is not stated.
- Parks nothing; nothing is built for it.

**Q6 (Tier 2), void by D-225: no reports key remains.**
How `FixedSecretSource` answers `smsReportKey` (D-224's former item 24).

- LIB-TEST-003's list omits it; without it no `FixedSecretSource` deployment starts
  (INT-GEN-002), the sample host's included.
- Reading A, built on `part/sms`: a public `SmsReportKey { get; init; }` the test gives,
  as the maintenance credential is given; absent, the key is unavailable. Smallest fix:
  the member in LIB-TEST-003's list.
- Reading B: drawn for the instance as its two keys are, with a public getter so a test
  can address the route. Smallest fix: the member and its draw in LIB-TEST-003.
- Either reading changes `Janus.Testing`'s public surface.
- Held `part/sms`, unmerged; the branch is deleted.

**Q7 (Tier 2), void by D-225: no gateway identifier is kept.**
Where the gateway's identifier of a text is recorded (D-224's former item 23).

- `INotificationHandler.SendAsync` answers `Result`, so only the shipped handler sees
  `SmsAccepted`.
- Reading A, built on `part/sms`: that handler stores the identifier's SHA-256 in a unit
  of work of its own; where that write fails, the text is answered as not taken and
  carried again, so the person receives a second text, as when a settle fails today.
  Smallest fix: a sentence in INT-SMS-006.
- Reading B: answered taken anyway; no second text, and reports naming the text are then
  rejected. Smallest fix: a sentence in INT-SMS-006.
- Reading C: a host's replacement handler hands the identifier on. Smallest fix: a public
  change to `INotificationHandler` in `07`.
- Also built, for review: a report arriving before the hash is written is rejected; where
  two texts share one identifier, the lookup takes the one sent last.
- Held `part/sms`, unmerged; the branch is deleted.

**Q8 (Tier 3), answered by D-226 item 4.**
Where the pending authorization is held once the sign-in completes
(item 12).

- AUTH-SESS-012, `09` section 9, D-224 item 12 and BFF-CSRF-005a: the pending
  authorization is bound to the browser's pre-authentication session, and the
  continuation, which follows the completed sign-in, finds it there.
- BFF-CSRF-005a AC3, AUTH-SESS-006 and D-206: authentication rotates the
  pre-authentication session into the sign-in session; the completing answer removes its
  row and clears its cookie.
- When the browser follows `continueTo`, no pre-authentication session is left to hold
  or prove the binding. No chapter says whether the pending authorization outlives the
  rotation, or what carries it to the continuation.
- Parks: item 12 and the sample host's sign-on and back. `oidc.continuation.lifetime` is
  declared, read by nothing.

**Q9 (Tier 3), answered by D-226 item 5.**
Whether the cookie of a session the restore admits survives the answer
that reports its expiry (item 13).

- `17` BFF-STEP-001, `09` and D-224 item 13: a request on a session expired for
  inactivity is answered 401 `auth.session.expired` with `details.reauthenticate`
  `single-factor`; the frontend then restores it on `/auth/restore/begin` and
  `/auth/restore`, carried by the expired session's cookie and the CSRF token bound to
  it, and retries the interrupted request (FE-API-004).
- `17` section 10, stage 5 (D-162): a cookie that no longer resolves is cleared, and the
  request continues as one that carried none. The 401 that interrupts the request clears
  both cookies, so neither restore route can be carried.
- No chapter says whether a cookie the restore admits is one that no longer resolves;
  D-224 amends the stage 8 row and not stage 5's.
- Parks: item 13 and the sample host's restore in place.

**Q10 (Tier 2).** What `ISendOutcomes.ReportAsync` answers to a repeated failure report
(INT-SMS-005, D-225 item 2).

- What the spec says: it answers true where the reference names a message the library
  handed over, a repeated report changing nothing, and false where it names none.
- What the code holds: a failure releases the counts and removes the ledger's row, so a
  second failure report finds no row and answers false. It changes nothing, as AC3 asks
  (`INT_SMS_005_AC3_ARepeatedReportChangesNothingAsync`, which asserts no answer).
- Reading A: true, the row keeping a released marker. Smallest fix: the marker, and the
  answer asserted.
- Reading B: false, as built. Smallest fix: a sentence in INT-SMS-005.
- Parks: only the answer's value.

**Q11 (Tier 2).** Whether `invalid_scope` covers the four scopes (AUTH-OIDC-001 AC5,
D-226 item 1).

- What the spec says: besides `openid`, `email`, `profile` and `offline_access`, a client
  receives each scope it is registered with, and a request for a scope its client is not
  registered with is refused with `invalid_scope`.
- What the code holds: a scope beyond the four is admitted where the client is
  registered with it and refused `invalid_scope` otherwise, the deployment's scope rows
  no longer admitting one. The four go to the server's own checks as before: `email` or
  `profile` asked by a client not registered with it is refused `invalid_request`, and
  `openid` and `offline_access` are not checked against the registration.
- Reading A: the clause speaks of the scopes beyond the four, as built. Smallest fix:
  none, or a sentence in AUTH-OIDC-001.
- Reading B: it covers every scope. Smallest fix: the four judged by the same check.
- Parks: only the four's refusal.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (4900 in eleven projects), the commit-message gate
  passes over the round's commits, and the full-history secret scan finds nothing. Two
  unpushed messages were brought into form first: a `style` type CONV-VCS-003 does not
  list (now `chore(core)`), and a body line of 73 characters in the sending commit.
- Verified by a run on this machine, besides the fast checks: the
  `JmapMailServerAgainstStalwartTests` against the pinned server, INT-MAIL-010's two
  among them (16 passed). Every other test of the round's integration suites runs first
  in the pipeline.
- Pipeline on 6f21aa88: red in the contract, truth-table and integration jobs.
  - Contract: `GET /settings` was missing from the gate's list; 060a18a4 (section 3).
  - Truth-table and integration: 4a388b52 required the provider address to build the
    provider's options, so a deployment declaring none failed before the startup check
    could refuse it, and `AUTH_SESS_012_AC3_ADeploymentThatDeclaredNoSignInScreenIsRefusedAsync`
    failed with every test on the shared host. 5b97685a leaves that deployment to the
    startup refusal; 1343bb5d declares the address in the harness that starts the
    provider alone (section 3).
  - The tests these jobs named pass on this machine on 1343bb5d.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
