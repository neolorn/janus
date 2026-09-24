# Corrections 3: D-164 and D-165

Status: complete on the development machine, no open question; the pipeline run of the
full gate waits on the branches being pushed (section 5). The corrected documentation
through D-165 is the first commit. D-165 is applied: the shipped register is the four
rows the library makes true, the retired items and every business word are gone from
code, tests, fixtures, comments, the changelog and `NOTICE`, and the machine-profile
pipeline is the seam a host mounts its own callbacks on, proved with a fake host
callback. D-164 is applied: the RFC 9700 and OAuth 2.1 conformance suite, pushed
authorization requests required for every client, RFC 9068 access tokens, and the
provider security events of IDN-LIFE-012a. The two gaps of the phase 8 report are
closed. The decisions taken in the owner's absence are in section 4 and, in the same
words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Instruction | Items | Tests |
|---|---|---|
| D-165, the shipped register is the four library-true rows (ledger 270) | PRIV-ROPA-002, INT-GEN-004 | `ProcessingRecordsTests.PRIV_ROPA_002_TheShippedRegisterIsTheFourRowsTheLibraryMakesTrue`, `ProcessingRecordsTests.PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync`, `ProcessingRecordsTests.INT_GEN_004_AC1_AProviderAddedWithoutAnAgreementReferenceIsFlaggedAsync` |
| D-165, `POST /callbacks/payment` and `POST /callbacks/shipping` | none | No endpoint, handler, rate limit key, error code or catalogue row for either existed in code: entry 72 had carried both callbacks past phase 6. What named them were the register rows, the second register type `Recipients` with its tests, fixtures and comments, all removed. `GET /callbacks/sms/dlr` is kept |
| D-165, retired items and the business words (ledger 271) | PRIV-SENS-001, PRIV-SENS-002, PRIV-SENS-002a, PRIV-CONS-007, PRIV-RIGHT-005, IDN-LIFE-002a, IDN-ATTR-007, IDN-LIFE-003 | The tests that carried PRIV-SENS-003 now carry the criteria they prove: `ConsentGateTests.PRIV_SENS_001_AC1_TheSensitiveDeclarationDerivesTheWrittenRequirementAsync`, `ConsentGateTests.PRIV_SENS_002_AC1_TheWrittenRecordIsAskedForByTheConsentBasedPurposeOnlyAsync`, `ConsentGateTests.PRIV_SENS_002a_AC1_TheRecordIsReadForASubjectWhoConsentedToNothingAsync`, `ConsentTests.PRIV_SENS_002a_AC1_AWithdrawalInvokesNoHandlerOfTheOtherPurposesAsync`, `ConsentGateTests.PRIV_CONS_007_AC3_ARevisedNoticeInterruptsNoContractualProcessingAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC5_AggregatesOverPlainColumnsAreUnchangedByAnErasureAsync`, `SubjectKeyStoreTests.IDN_LIFE_002a_AC2_AThirdPartysFieldsGoWithTheSubjectWhoEnteredThemAsync`. Verified by search: a repository-wide search outside `docs/` for INT-PAY, INT-SHIP, PRIV-SENS-003, PRIV-SENS-004, PRIV-MIN, FE-ADDR, INF-DB-002, R-A06, payment, courier, cart, checkout, cash on delivery, shipping, shipment, order, product, purchase, refund, fulfilment, invoice, basket and dispatch finds nothing in the business sense. The senses it passes: a sequence (`the order is fixed`, `in table order`, `byte order mark`, `ORDER BY`, the normalizer's `Order`), the provider framework's handler `Order` constants and its `IOpenIddictServerDispatcher`, `DispatchAsync` of a registration code, the lawful basis `court-judgment-or-order` of chapter 10, the product name of CONV-NAME-001, the fulfilment of an erasure request, and the pipeline's pinned `actions/checkout` |
| D-165, section citations of the renumbered 04 and 05 | the chapters' own numbering | Every citation in code and tests names `05` section 6 for the provider register; no code cites a section of `04` by number |
| D-165, the machine-profile seam, with a fake host callback (ledger 276) | BFF-MACH-001, BFF-MACH-002, BFF-MACH-003, INT-GEN-003, CONV-DESIGN-005 | `HostCallbackTests.BFF_MACH_001_AC2_AHostCallbackCarryingASessionCookieIsRefusedAsync`, `BrowserProfileTests.BFF_MACH_001_AC3_ARequestTheMachineProfileGovernsPassesTheBrowserProfileAsync`, `HostCallbackTests.BFF_MACH_002_AC1_AnUnsignedOrMisSignedCallbackIsRejectedBeforeParsingAsync`, `HostCallbackTests.BFF_MACH_002_AC2_AReplayOutsideTheWindowIsRejectedAsync`, `HostCallbackTests.BFF_MACH_002_AC3_ADuplicateEventIdentifierIsProcessedOnceAsync`, `HostCallbackTests.BFF_MACH_002_AC3_ADeliveryTheRouteFailedIsCarriedAgainAsync`, `HostCallbackTests.BFF_MACH_002_AC3_ADeliveryCarryingNoEventIdentifierIsRefusedAsync`, `HostCallbackTests.BFF_MACH_002_AC4_ComparisonIsConstantTime`, `HostCallbackTests.BFF_MACH_002_TheSecretReplacedVerifiesFor24HoursAsync`, `HostCallbackTests.BFF_MACH_002_SecretsTheManagerCannotGiveVerifyNothingAsync`, `HostCallbackTests.BFF_MACH_002_AnAlgorithmThePlatformCannotComputeFailsWhenMounted`, `CallbackStoreTests.BFF_MACH_002_AC3_AnEventIsClaimedOnceAsync`, `CallbackStoreTests.BFF_MACH_002_AC3_AnEventGivenBackIsClaimedAgainAsync`, `HostCallbackTests.BFF_MACH_003_AC1_NoUnsignedCallbackAdvancesStateWithoutConfirmationAsync`, `HostCallbackTests.BFF_MACH_003_AC2_AForgedCallbackWithAGuessedReferenceIsRejectedAndLoggedAsync`, `HostCallbackTests.BFF_MACH_003_AC3_RepeatedVerificationFailuresRaiseAnAlertAsync`, `HostCallbackTests.INT_GEN_003_AFloodIsAnsweredBeforeAnyLookupAsync`, `HostCallbackTests.INT_GEN_003_ACallbackFromOutsideThePublishedRangesIsRejectedAsync`, `HostCallbackTests.INT_GEN_003_AReferenceIs128RandomBitsKeptByItsHashAsync`, `CallbackStoreTests.INT_GEN_003_AReferenceIsHeldForItsCallbackOnlyAsync`, `CallbackStoreTests.INT_GEN_003_TheTablesHoldHashesAndTimesAndNothingElseAsync`, `CallbackContractTests.CONV_DESIGN_005_AC1_EveryCallbackContractMethodReturnsAnOutcome`, `CallbackContractTests.CONV_DESIGN_005_AC2_NoCallbackContractReturnsNull` |
| D-165, `GET /callbacks/sms/dlr` on the same pipeline (ledger 277, 278) | INT-SMS-005, INT-GEN-003, BFF-MACH-001 | `DeliveryReportEndpointTests.BFF_MACH_001_AC3_TheDeliveryReportIsCarriedOnTheMachineProfileAsync`, `DeliveryReportEndpointTests.BFF_MACH_001_AC2_ADeliveryReportCarryingASessionCookieIsRefusedAsync`, `DeliveryReportEndpointTests.INT_GEN_003_AC1_AForgedOrUnreadableReportIsRejectedAsync`, `DeliveryReportsTests.INT_GEN_003_AC1_ACallbackWithAGuessedReferenceIsRejectedAsync`, `SendLedgerTests.HoldsAsync_ASendCounted_IsHeldUntilItIsReleasedAsync` |
| Corrected chapters, BFF-LOG-002 (ledger 274) | BFF-LOG-002, CONV-LOG-003 | `SensitiveBodyLoggingTests.BFF_LOG_002_AC1_BodyLoggingIsOffByDefault`, `SensitiveBodyLoggingTests.BFF_LOG_002_AC1_EveryEndpointTheLibraryMapsIsMarked`, `SensitiveBodyLoggingTests.BFF_LOG_002_AC1_ABodyMetBeforeRoutingIsNotLoggedAsync`, `SensitiveBodyLoggingTests.BFF_LOG_002_AC2_NoLogEntryHoldsAFieldOfAMarkedEndpointsBodyAsync` |
| Corrected chapters, BFF-CSRF-005 AC4 (ledger 275) | BFF-CSRF-005 | `BrowserProfileTests.BFF_CSRF_005_AC4_ACrossSitePostReturnContinuesAsTheHostsGetAsync`, `BrowserProfileTests.BFF_CSRF_002_AC1_ACrossSitePostIsRejectedBeforeAnyEndpointAsync` |
| D-164 item 1, RFC 9700 and OAuth 2.1 stated and proved (ledger 280) | AUTH-OIDC-006 AC1 | `ProviderConformanceTests.AUTH_OIDC_006_AC1_TheImplicitFormsAreRefusedAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_EveryOtherGrantIsRefusedAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_OnlyTheS256ProofKeyIsTakenAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_OnlyTheExactRegisteredDestinationReceivesTheCodeAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_ACodeIsNotExchangedForAnotherDestinationAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_NoPublicClientReceivesARefreshTokenAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC1_TheDocumentNamesOnlyWhatIsAdmittedAsync` |
| D-164 item 2, pushed authorization requests at `/oidc/par`, required (ledger 279) | AUTH-OIDC-006 AC2, AC4 | `ProviderConformanceTests.AUTH_OIDC_006_AC2_ADirectAuthorizationRequestIsRefusedAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceIsTakenOnceAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceAnsweredWithoutACodeIsSpentAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC2_AReferenceLapsesAfterSixtySecondsAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC4_TheDocumentRequiresPushedRequestsAsync`, `SignOnTests.AUTH_OIDC_006_AC2_TheBrowserCarriesOnlyThePushedReferenceAsync`, `SignOnTests.AUTH_OIDC_006_AC2_ARequestThePushRefusesIsNotForwardedAsync`, `OidcStoreTests.AUTH_OIDC_006_AC2_APushedRequestIsKeptNamingNobodyAsync` |
| D-164 item 3, RFC 9068 access tokens (ledger 281) | AUTH-OIDC-006 AC3 | `ProviderConformanceTests.AUTH_OIDC_006_AC3_EveryAccessTokenIsTypedAndCarriesTheSevenClaimsAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC3_ARefreshedAccessTokenIsTypedAndCarriesTheSevenClaimsAsync`, `ProviderConformanceTests.AUTH_OIDC_006_AC3_TheAdapterRefusesATokenForAnotherAudienceAsync`, `AppPasswordFlowTests.AUTH_OIDC_006_AC3_TheMailServersTokenIsOneItsAdapterTakesAsync` |
| D-164 item 4, provider security events at `POST /callbacks/providers/{provider}` (ledger 282 to 289) | IDN-LIFE-012a | `ProviderEventTests.IDN_LIFE_012a_AC1_ASignedCompromiseEndsEverySessionAndHoldsTheCredentialAsync`, `ProviderEventTests.IDN_LIFE_012a_AC1_AnUnsignedEventChangesNothingAndIsAuditedAsRejectedAsync`, `ProviderEventTests.IDN_LIFE_012a_AC1_AReplayedEventChangesNothingAndIsAuditedAsRejectedAsync`, `ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnIdentityIsUnlinkedAsync`, `ProviderEventTests.IDN_LIFE_012a_AC2_AWithdrawnLastCredentialSuspendsTheAccountWithANoticeAsync`, `ProviderEventTests.IDN_LIFE_012a_AC3_TheEndpointIsOnTheMachineProfileAsync`, `ProviderEventTests.IDN_LIFE_012a_AC3_TheEndpointIsRateLimitedLikeEveryCallbackAsync`, `ProviderEventTests.IDN_LIFE_012a_AHeldCredentialStandsAgainOnceThePersonSignsInByAnotherFactorAsync`, `ProviderEventTests.IDN_LIFE_012a_AnAddressTheProviderStoppedForwardingToDropsToUnverifiedAsync`, `ProviderEventTests.IDN_LIFE_012a_AnEventThatChangesNothingIsRecordedAndAcknowledgedAsync`, `ProviderEventTests.IDN_LIFE_012a_AnEventNothingDeclaredOrReadableVerifiesIsRefusedAsync`, `SessionServiceTests.IDN_LIFE_012a_AHeldCredentialStandsAgainAtASignInByAnotherFactorAsync`, `IdentifierSetTests.IDN_LIFE_012a_AnUnvouchedAddressDropsToUnverifiedAndHandsThePrimaryOn`, `IdentifierStoreTests.IDN_LIFE_012a_AnUnvouchedAddressDropsToUnverifiedAsync`, `AuthenticatorStoreTests.IDN_LIFE_012a_ALinkedIdentityIsFoundByTheProvidersSubjectAsync`, `AuthenticatorStoreTests.IDN_LIFE_012a_AProvidersSubjectIsLinkedOnceAsync`, `AuthenticatorStoreTests.IDN_LIFE_012a_OnlyALinkedIdentityHoldsAProvidersSubjectAsync`, `AuthenticatorStoreTests.IDN_LIFE_012a_AHeldCredentialReadsBackHeldAsync`, `SubjectEraserTests.IDN_LIFE_012a_TheProvidersSubjectOfALinkedIdentityGoesWithItsHolderAsync`, `StartupValidationTests.IDN_LIFE_012a_ASocialProviderDeclaredShortOfWholeIsRefusedAsync` |
| Gap 1, `[NeverLogged]` on every carrier CONV-LOG-003 names, and JAN0002 shown to fire (ledger 272) | CONV-LOG-003 AC1, CONV-CODE-008 | `NeverLoggedValueAnalyzerTests.CONV_LOG_003_AC1_TheLibrarysOwnCarriersAreReportedAsync`, `NeverLoggedValueAnalyzerTests.CONV_LOG_003_AC1_TheVersionAndTheIdentifiersBesideThemAreNotReportedAsync`, `NeverLoggedTests.CONV_LOG_003_AC1_EveryTypeCarryingAForbiddenValueIsMarked`, `NeverLoggedTests.CONV_LOG_003_AC1_EveryMemberCarryingAForbiddenValueIsMarked`, `NeverLoggedTests.CONV_LOG_003_AC1_EveryColumnCarryingAForbiddenValueIsMarked` |
| Gap 2, `Settings.ThrowIfIncomplete` where LIB-HOST-001 AC2 runs it, with the failure path (ledger 273) | LIB-HOST-001 AC2, AC3, AC4 | `StartupValidationTests.LIB_HOST_001_AC2_AnUnnamedKeyIsRefusedByNameBeforeTheServerStartsAsync`, `StartupValidationTests.LIB_HOST_001_AC2_AConditionalKeyIsRefusedOnceItsConditionHoldsAsync`, `StartupValidationTests.LIB_HOST_001_AC3_ADeploymentNamingOnlyTheListedKeysStartsAsync`, `StartupValidationTests.LIB_HOST_001_AC4_ADeploymentWithoutItsGoverningLanguageIsRefusedAsync` |

The ledger entries D-164 and D-165 reverse or settle each carry one line: entries 62,
72, 103 and 151 "Superseded by D-165", entry 262 "Superseded by D-164".

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTH-OIDC-006 AC3, the adapter half | No mail server adapter ships in Milestone 1 (entry 215). The half is proved by a verifier configured as the adapter must be, against what the provider publishes; the adapter is held to the same test (entry 281) | Milestone 2 step 5 |
| AUTH-OIDC-006 AC1 in `Janus.Conformance` | The suite runs in the library's own tests; carrying the provider's refusals into the host-run conformance package is phase 10's (entry 280) | Phase 10 |
| IDN-LIFE-012a, a linked identity made by a sign-in | The events act on a credential linked to a provider's subject, which the store holds and the tests link directly. Social sign-in and linking (IDN-LIFE-012, REG-IDENT-008, `/account/link/{provider}`) is not built; no phase's chapter list names it | Phase 10, the exit-gate coverage pass |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Hosting.Tests/Authorization/HostFixture.cs` | With `Settings.ThrowIfIncomplete` run at startup, the fixture's deployment named too few keys to start | the working guide's section 3, test infrastructure; LIB-HOST-001 AC2 | The fixture seeds every key LIB-HOST-001 requires with a value of the kind a deployment gives it |
| `tests/Janus.Hosting.Tests/Deployment.cs` | The harness served no social provider's documents, registered no callback admission and exposed neither the credential audit nor the send ledger | the working guide's section 3, test infrastructure | The harness declares Google and Apple against in-memory metadata and keys (`SocialProvidersInMemory`), registers the callback admission over in-memory callback ledgers, and exposes the credential audit and the send ledger |

## 4. Decided in the owner's absence

Under D-161 as amended by D-162. Each entry is in `docs/reports/decisions-pending-review.md`
in the same words, numbered as it is there.

| # | Tier | Decision |
|---|---|---|
| 270 | 2 | The shipped provider register is four rows; the developer row is the host's |
| 271 | 2 | What the D-165 word search covers |
| 272 | 2 | What carries `[NeverLogged]` |
| 273 | 2 | Where the required keys are checked, and when the register counts as generated |
| 274 | 2 | How a sensitive body is kept out of request logging |
| 275 | 3 | What the browser profile does with a processor's cross-site post return |
| 276 | 2, 3 where marked | The seam a host mounts its own callbacks on |
| 277 | 3 | A report of delivery is held to a live send |
| 278 | 2 | How the delivery report is read and what it answers |
| 279 | 2 | When a pushed request is spent, and where its destination is judged |
| 280 | 2 | What the provider's conformance suite is and what it asserts |
| 281 | 3 | An access token's audience, and the adapter that verifies it |
| 282 | 3 | The provider event endpoint beside chapter 09's one callback |
| 283 | 3 | What a deployment declares of a social provider, and how its keys are read |
| 284 | 2 | How a provider's event finds the account it concerns |
| 285 | 3 | Which provider events do what |
| 286 | 3 | A withdrawn identity that is the last way in |
| 287 | 3 | A disabled address that is the personal email a membership keeps |
| 288 | 2 | How a credential is held until the person signs in by another factor |
| 289 | 2 | How a provider's event is carried once and audited |

## 5. Gate result

`corrections-3` is cut from the head of `phase-08-organizations` (`8bebad9`), not from a
merged `main`: the phase 8 pull request could not be opened from the development
machine, and opening it, merging it and pushing this branch wait on the owner's
permission. The docs commit is the branch's first.

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests. `dotnet test` still reports that no tests ran on the development machine, as
phase 0 records, so the suites were run by executing the test binaries. The local
counts at the end of the branch, unit and contract: `Janus.Analyzers.Tests` 17,
`Janus.Authentication.Tests` 670, `Janus.Authorization.Tests` 114, `Janus.Core.Tests`
437, `Janus.Hosting.Tests` 488, `Janus.Identity.Tests` 76, `Janus.Privacy.Tests` 185 and
`Janus.Storage.Tests` 33; integration: `Janus.Hosting.Tests` 148 and
`Janus.Storage.Tests` 329; none failing.

The full gate was run once on the development machine, all green: the integration
suites above against containers; the contract suite (71); the policy coverage test (3);
the truth-table suite (49); the migrations applied from empty to two throwaway
PostgreSQL 17 databases created as the double-migration job creates them, with no
release tagged yet so the second run also starts from empty, and applied again with
nothing left to apply; no model change without its migration; the Unicode tables
regenerate without a diff; locked restore; no referenced package with a known
vulnerability; and the forbidden-marker, acceptance-criterion test name, commit
message and changelog jobs over `8bebad9..HEAD`.

The pipeline run, which is the gate of record, has not been made: the branch is not
pushed.
