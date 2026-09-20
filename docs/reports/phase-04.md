# Phase 4: Sending and restrictions

Status: complete, no open question, full gate pending the pipeline run. One path for every message the
library sends, the catalogue and the transports behind ports a host registers, the
named restrictions with their keys, purposes, buckets, grants and runtime edits, the
progressive delay, the non-existence notice, the bot defence, the prepaid balance and
its floor, the delivery-report callback, the alert conditions with their deduplication,
their two channels and their destination change, and the settings table every runtime
key is now read from are built. The decisions taken in the owner's absence are in
section 4 and, in the same words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTH-ABUSE-001 | AC1 to AC6 | `ThrottleServiceTests.AUTH_ABUSE_001_AC1_RepeatedFailuresRaiseTheDelayAndDisableNothingAsync`, `ThrottleServiceTests.AUTH_ABUSE_001_AC2_TheDelayDecaysWithTimeAsync`, `ThrottleServiceTests.AUTH_ABUSE_001_AC3_ADistributedAttackIsCaughtByTheAccountComponentAsync`, `ThrottleServiceTests.AUTH_ABUSE_001_AC4_TheAccountDelayStopsAtItsCapAsync`, `ThrottleServiceTests.AUTH_ABUSE_001_AC5_ARecognisedBrowserIsNotHeldByAnAttackAsync`, `ThrottleServiceTests.AUTH_ABUSE_001_AC6_TheIdentifierComponentSharesTheAccountTerms` |
| AUTH-ABUSE-002 | AC1, AC2, AC3 | `ThrottleServiceTests.AUTH_ABUSE_002_AC1_TheDelayIsTheSameWhetherTheAccountExistsOrNotAsync`, `ThrottleServiceTests.AUTH_ABUSE_002_AC2_TheRemainingDelayIsCommunicated`, `SendingServiceTests.AUTH_ABUSE_002_AC3_ARefusedSendAnswersTheSameForEitherAddressAsync` |
| AUTH-ABUSE-003 | AC3, AC4, AC5 | `NonExistenceNoticeTests.AUTH_ABUSE_003_AC3_TheAddressIsToldAndTheMessageNamesNobodyAsync`, `NonExistenceNoticeTests.AUTH_ABUSE_003_AC4_ASecondNoticeInsideTheWindowIsSuppressedAsync`, `NonExistenceNoticeTests.AUTH_ABUSE_003_AC5_AnUnusualRateRaisesTheProbeAlertAsync` |
| AUTH-ABUSE-004 | AC1 to AC7 | `SendingServiceTests.AUTH_ABUSE_004_AC1_AFourthTextMessageInsideADayIsRefusedWithTheLiftAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC1_ASecondMailInsideAMinuteIsRefusedWithTheLiftAsync`, `SettingsTests.AUTH_ABUSE_004_AC1_TheShippedRestrictionsAreTheNamedFour`, `DeliveryReportsTests.AUTH_ABUSE_004_AC2_AFailureReportReleasesEveryBucketAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC2_ATransportRefusalCountsNothingAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_AnEditWithoutStepUpIsRefusedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_AnEditAppliesToTheNextSendAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_AnEditIsAuditedAndAnnouncedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_ALooseningRaisesANormalAlertAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC3_ALooseningWithoutAReasonIsRefusedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC4_AGrantWithoutAReasonIsRefusedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC4_AGrantAddsCreditAndIsAuditedAndAnnouncedAsync`, `RestrictionAdministrationTests.AUTH_ABUSE_004_AC4_AGrantOnNothingIsRefusedAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC4_AGrantedKeyIsRefusedOnceTheCreditIsSpentAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC5_ANoticeToAHolderPassesADrainedDestinationAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordHoldsAHashAndTimesAndNothingElseAsync`, `SendLedgerTests.AUTH_ABUSE_004_AC6_TheRecordIsGoneOnceItsBucketsAreEmptyAsync`, `SendingServiceTests.AUTH_ABUSE_004_AC7_AHostSuppliedKeyIsEvaluatedLikeTheBuiltInOnesAsync` |
| AUTH-ABUSE-005 | AC1, AC2, AC3 | `MessageBudgetTests.AUTH_ABUSE_005_AC1_ALatinMessageHoldsOneHundredAndSixty`, `MessageBudgetTests.AUTH_ABUSE_005_AC1_AnArabicMessageHoldsSeventy`, `SendingValidationTests.AUTH_ABUSE_005_AC2_EveryMessageIsCheckedInEveryLanguageAsync`, `SendingValidationTests.AUTH_ABUSE_005_AC3_AnOverBudgetTextMessageStopsStartupAsync`, `SendingValidationTests.AUTH_ABUSE_005_AC3_ALanguageTheCatalogueCannotAnswerInStopsStartupAsync`, `StartupValidationTests.AUTH_ABUSE_005_AC3_ADeploymentThatDeclaredNoMessagesIsRefusedAsync` |
| AUTH-ABUSE-006 | AC1, AC2 | `SmsBalanceTests.AUTH_ABUSE_006_AC1_AnAbnormalHourOfSpendRaisesTheAlertAsync`, `SmsBalanceTests.AUTH_ABUSE_006_AC1_AFloorWithinADayRaisesTheAlertAsync`, `SmsBalanceTests.AUTH_ABUSE_006_AC2_AtTheFloorTheHardStopIsInForceAsync` |
| AUTH-ABUSE-007 | AC1, AC2 | `DeliveryReportsTests.AUTH_ABUSE_007_AC1_AForgedReferenceIsRejectedAsync`, `DeliveryReportsTests.AUTH_ABUSE_007_AC2_AReportOfDeliveryChangesNothingAsync` |
| AUTH-ABUSE-008 | AC1 to AC4 | `BotDefenceTests.AUTH_ABUSE_008_AC1_AnOrdinaryRegistrationPresentsNoChallengeAsync`, `BotDefenceTests.AUTH_ABUSE_008_AC2_ADatacenterSourcePresentsAChallengeAsync`, `BotDefenceTests.AUTH_ABUSE_008_AC3_RepeatedAttemptsFromOneSourcePresentAChallengeAsync`, `BotDefenceTests.AUTH_ABUSE_008_AC4_WithAVerifierOnlyAPassingTokenCompletesAsync`, `BotDefenceTests.AUTH_ABUSE_008_AC4_WithNoVerifierTheSignalIsAuditedAndNothingIsShownAsync` |
| INT-GEN-001 | AC1, AC2 | `SendingValidationTests.INT_GEN_001_AC1_APlaintextEndpointStopsStartupAsync`, `SendingValidationTests.INT_GEN_001_AC1_EveryEndpointOverTlsStartsAsync`, `IntegrationEndpointsTests.INT_GEN_001_AC1_AnAddressIsInsecureUnlessItIsHttps`, `IntegrationEndpointsTests.INT_GEN_001_AC2_APlaintextAddressIsNamedWithItsIntegrationAndKey` |
| INT-GEN-003 | AC1, AC2, AC3 | `DeliveryReportsTests.INT_GEN_003_AC1_ACallbackWithAGuessedReferenceIsRejectedAsync`, `DeliveryReportsTests.INT_GEN_003_AC2_ACallbackAdvancesNoStateOfItsOwnAsync`, `DeliveryReportsTests.INT_GEN_003_AC3_ARejectedCallbackIsRecordedWithItsSourceAsync` |
| INT-GEN-004 | AC1 | `RecipientsTests.INT_GEN_004_AC1_TheShippedRegisterIsFlaggedUntilItIsEdited`, `RecipientsTests.INT_GEN_004_AC1_AProviderWithoutAnAgreementReferenceIsFlagged` |
| INT-GEN-005 | AC1, AC2 | `SendingServiceTests.INT_GEN_005_AC1_AnOutboundMailCarriesItsExactFieldSetAsync`, `SendingServiceTests.INT_GEN_005_AC1_AnOutboundTextMessageCarriesItsExactFieldSetAsync`, `IntegrationBoundaryTests.INT_GEN_005_AC2_NoCallSiteBuildsAnOutboundPayload` |
| INT-SMS-001 | AC4 | `SendingServiceTests.INT_SMS_001_AC4_ANoticeToAHeldPhoneAnswersToNotificationOnlyAsync` |
| INT-SMS-003 | AC1, AC2 | `SendingValidationTests.INT_SMS_003_AC1_ALatinMessageOverItsBudgetStopsStartupAsync`, `SendingValidationTests.INT_SMS_003_AC2_EveryTextMessageIsMeasuredInEveryLanguageAsync` |
| INT-SMS-004 | AC1, AC2 | `SmsBalanceTests.INT_SMS_004_AC1_TheScheduledPollIsWhatRaisesTheAlertAsync`, `SendingServiceTests.INT_SMS_004_AC2_AnOrdinarySendIsRefusedBelowTheFloorAsync` |
| INT-SMS-005 | AC1, AC2, AC3 | `DeliveryReportsTests.INT_SMS_005_AC1_AForgedReportVerifiesNoPhoneAsync`, `DeliveryReportsTests.INT_SMS_005_AC2_ACorrelationReferenceIsUnguessable`, `DeliveryReportsTests.INT_SMS_005_AC3_ASettledOrUnknownReferenceChangesNothingAsync` |
| INT-SMS-006 | AC1 | `IntegrationBoundaryTests.INT_SMS_006_AC1_NoProviderNameAppearsInTheLibrary` |
| OPS-ALERT-001 | AC1, AC2 | `ThrottleServiceTests.OPS_ALERT_001_AC1_SustainedFailuresAgainstOneAccountRaiseTheAlertAsync`, `DeliveryReportsTests.OPS_ALERT_001_AC1_RepeatedRejectionsRaiseTheCallbackAlertAsync`, `RestrictionAdministrationTests.OPS_ALERT_001_AC1_AGrantRaisesTheRestrictionGrantedAlertAsync`, `AlertsTests.OPS_ALERT_001_AC2_TheConditionsAreTheTableInOrder` |
| OPS-ALERT-002 | AC1 | `AlertRouterTests.OPS_ALERT_002_AC1_AThousandFailuresProduceOneAlertAsync`, `AlertRouterTests.OPS_ALERT_002_AC1_TheSameConditionOnTwoAccountsIsTwoAlertsAsync`, `AlertsTests.OPS_ALERT_002_AC1_TheDeduplicationKeyIsTheConditionAndWhoseItIs` |
| OPS-ALERT-003 | AC1 to AC4 | `AlertRouterTests.OPS_ALERT_003_AC1_AMailSystemFailureStillReachesSomeoneAsync`, `AlertRouterTests.OPS_ALERT_003_AC2_ABalanceFloorBreachIsReportedDespiteTheStopAsync`, `SendingServiceTests.OPS_ALERT_003_AC3_AnAlertIsSentBelowTheFloorAsync`, `AlertRouterTests.OPS_ALERT_003_AC4_AGatewayThatCarriesNothingIsRecordedAsync` |
| OPS-ALERT-004 | AC1, AC2, AC3 | `AlertRouterTests.OPS_ALERT_004_AC1_EveryConfiguredDestinationIsReachedAsync`, `AlertRouterTests.OPS_ALERT_004_AC2_EnablingOwnerNotificationNeedsNoDeployAsync`, `AlertRouterTests.OPS_ALERT_004_AC3_ABreakGlassUseReachesTheOwnerRegardlessAsync` |
| OPS-ALERT-004a | AC1 to AC5 | `AlertDestinationChangeTests.OPS_ALERT_004a_AC1_EveryPreviousDestinationIsNotifiedAsync`, `AlertDestinationChangeTests.OPS_ALERT_004a_AC2_TheNoticeAnswersToNoSettingAsync`, `AlertDestinationChangeTests.OPS_ALERT_004a_AC3_TheLastDestinationCannotBeRemovedAsync`, `AlertDestinationChangeTests.OPS_ALERT_004a_AC4_TheNoticeReachesThePreviousDestinationsAsync`, `AlertDestinationChangeTests.OPS_ALERT_004a_AC5_AnEmptyListIsRefusedWithTheNamedCodeAsync` |
| OPS-CFG-008 | AC1, AC3, AC4 | `ConfigurationStoreTests.OPS_CFG_008_AC1_AChangedSettingIsInForceForTheNextReadAsync`, `ConfigurationStoreTests.OPS_CFG_008_AC3_NoBootstrapValueIsAKeyOfTheTable`, `RestrictionAdministrationTests.OPS_CFG_008_AC4_AnEditIsLiveAuditedWithBothValuesAndAlertedAsync` |
| AUTH-FACT-016 (carried from phase 3) | AC4 in part, AC5 | `SendingServiceTests.AUTH_FACT_016_AC4_TheCheckCodeCountsAgainstTheEmailDestinationAsync`, `DeviceServiceTests.AUTH_FACT_016_AC5_ThePassedCheckIsAnnouncedOnceAsync` |
| LIB-API-001 | AC2, for the vocabularies this phase adds | `VocabularyContractTests.LIB_API_001_AC2_TheRestrictionVocabulariesAreTheContract`, `VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten` |
| REG-ACCT-001 | AC2, extended over the tables this phase adds | `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` |

Also built and reported here because phase 3 deferred it: the registration of the
phase 3 authentication services in `AddJanus` (section 4, decision 33), and the
implementation of `IConfigurationStore` over the `settings` table, which was the reason
for that deferral.

Criteria a test cannot decide, named here as CONV-TEST-007 requires:

| Criterion | How it was verified |
|---|---|
| AUTH-ABUSE-003 AC2, that response timing does not vary measurably with existence | By construction, as the criterion itself directs. One code path answers for both cases: the throttle reads a keyed hash whether or not an account holds the identifier, the delay is computed from the counters alone, and the refusal carries the same code and the same `retryAt` either way. Asserted through criterion 1's byte-identical answers, which phase 5 tests at the endpoint |
| INT-GEN-002 AC1, that no provider credential appears in any repository file | By construction and by the pipeline. No source file of the library reads an environment variable or a configuration file for a credential, and no type of the library declares one: a transport is a port the host implements, and what it presents to a gateway never crosses the boundary. The repository itself is covered by the secret-scanning job of CONV-GATE-001 |
| INT-GEN-002 AC2, that rotating a credential requires no code change | By construction. The library names no credential, so a rotation is a change to the host's registration of `IMailTransport` or `ISmsTransport` and reaches no library code |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTH-ABUSE-003 AC1 | Byte-identical responses for an existing and a non-existent address are decided at the endpoint, and no endpoint of the recovery, sign-in-link or email-code paths exists yet. The refusal the endpoint will carry is decided here and tested through AUTH-ABUSE-002 AC3 | Phase 5, and phase 6 for recovery |
| AUTH-ABUSE-003 AC6 | The wording and the routes of the duplicate-address notice are the registration flow's, under REG-SESS-005 | Phase 5 |
| AUTH-FACT-016 AC4, the primary-email half | The code counting against `email.destination` and the refusal are decided here. That it goes to the **primary email only** reads the account's identifiers, which `Janus.Identity` holds and `Janus.Authentication` does not reference | Phase 5 |
| AUTH-FACT-016 AC7 | The browser that completed the terms step is recorded by the registration flow | Phase 5, REG-SESS-007 |
| AUTH-STEP-007 AC1 | The enrolment notification goes to every recorded channel, which is the security-notice set of REG-IDENT-002, held by `Janus.Identity`. The send it will make is built here | Phase 5 |
| INT-SMS-001 AC1, AC2 | Both are about what a sign-in path accepts and what it sends. Verification codes, sign-in links and second-step codes are issued by the sign-in and recovery flows, and no path issues one yet. That a verification code is no catalogue entry is decided in phase 3 (AUTH-FACT-004 AC1) | Phase 6 |
| INT-SMS-001 AC3 | The enrolment link is sent after an approver records an out-of-band confirmation, which is the admin-assisted re-enrolment of AUTH-RECOV-002 | Phase 6 |
| INT-GEN-002 | Storing provider credentials as rotatable secrets under the key-encryption key is the deployment's secrets manager. The library's half of it is in section 1 as verified by construction | Milestone 2, phase 4, INF-HOST-003 |
| OPS-CFG-002 AC3, AC4, and OPS-CFG-005 | A written reason and an audit entry for every runtime change with no direction needs a general configuration audit, which every change would reach through `IConfigurationStore`. The alert-destination change of OPS-ALERT-004a is stepped up and tested, but takes no reason and writes no entry | Section 4, decision 34 |
| OPS-CFG-008 AC2 | The same general configuration audit | Section 4, decision 34 |
| INT-SMS-002 | Retired by D-146 | Nothing |
| INT-SMS-005a | States that where message content lives is not specified, and carries no criteria | Nothing |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `src/Janus.Authentication/Alerting/AlertRouter.cs` | A notice addressed to one channel recorded the other channel as a gateway that carried nothing, so a single-channel alert wrote a false entry in the alert log | OPS-ALERT-003 AC4 | A channel the audience names no destination for was not asked to carry anything, so it is not the residual case the criterion describes |
| `.github/gates/acceptance-test-names.sh` | The job resolved an item to the first chapter that mentions it. `00` section 1.2 quotes AUTH-SESS-005 to show the requirement format and states none of its criteria, so on a run where the match returned `00` first every AUTH-SESS-005 test failed the gate | CONV-TEST-007 AC2, the working guide's section 3, a gate that mis-implements its own rule | The declaring chapter is the one that opens a line with the item; a chapter that quotes an item states none of its criteria |
| `src/Janus.Hosting/JanusRegistration.cs` | A prose comment ended in a semicolon, which the forbidden-marker gate reads as code someone commented out | CONV-CODE-005 AC2 | The gate checks what the chapter says; the sentence ends in a full stop |
| `tests/Janus.Authentication.Tests/Alerting/AlertRouterTests.cs` | A test named for what it does read as a public `Raise` method to CA1030 | CONV-CODE-008, CONV-TEST-007 | A test deciding a criterion is named `ITEM_ACn_Outcome`, which is also what the analyser accepts |
| `tests/Janus.Storage.Tests/Authentication/SendLedgerTests.cs` | The key separator was written as a raw NUL byte in the source, so every text tool read the file as binary and the gates that scan `tests` skipped it | CONV-GATE-001, the working guide's section 3, test infrastructure | A gate that reads the test sources has to be able to read them; the separator is written as the escape the code under test uses (` `), which changes nothing the test decides |
| `tests/Janus.Hosting.Tests/Authorization/HostFixture.cs`, `tests/Janus.Hosting.Tests/Sending/MessageTemplatesInMemory.cs` | The hosting fixtures declared no message catalogue and no notification language, so every hosting test failed the startup check this phase added | LIB-HOST-001, the working guide's section 3, test infrastructure | A fixture stands for a deployment that has declared what the library requires of one; the check itself is unchanged |

## 4. Decided in the owner's absence

Under D-161. Each entry is in `docs/reports/decisions-pending-review.md` in the same
words, numbered as it is there.

| # | Decision |
|---|---|
| 22 | Sending, restrictions and alerting are built in `Janus.Authentication` |
| 23 | A transport that would not take a message is a refusal to the caller; the durable retry is the outbox publisher's |
| 24 | A refused send carries `retryAt` and nothing else |
| 25 | A restriction change is a loosening unless every bucket it keeps is at least as strict |
| 26 | The text-message budget is measured over the template as the catalogue holds it |
| 27 | A plaintext endpoint is caught through a register the host declares, not a setting for each integration |
| 28 | The restriction administration is built as an operation; its endpoints wait for the phase that mounts the administrative surface |
| 29 | The event port publishes without an outcome; the consumer returns one |
| 30 | The message catalogue answers with an outcome, not with a missing template |
| 31 | A send counter is swept by the time its longest bucket settles at |
| 32 | A deployment that declares no message catalogue is refused at startup |
| 33 | The authentication services deferred from phase 3 are registered in this phase |
| 34 | OPS-CFG-008 is completed here except its general audit criterion, which waits with OPS-CFG-002 and OPS-CFG-005 (Tier 3) |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of the phase:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 351, `Janus.Authorization.Tests`
96, `Janus.Core.Tests` 413, `Janus.Hosting.Tests` 147, `Janus.Identity.Tests` 62,
`Janus.Privacy.Tests` 13 and `Janus.Storage.Tests` 198, none failing.

Full gate: the branch `phase-04-sending-2` is not yet pushed, so the pipeline has not
run. The jobs of CONV-GATE-001 that run without a database were run locally and are
green: commit message format, changelog line present, forbidden markers and
commented-out code, acceptance-criterion test names, the destructive-operation report,
and format. This section is completed with the run identifiers once the branch is
pushed.
