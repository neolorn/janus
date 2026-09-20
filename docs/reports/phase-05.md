# Phase 5: Registration and account management

Status: complete, full gate green, no open question. The pre-authentication session a
browser is given on first contact, the registration bound to it and its ten steps, the
age screen and the affirmation derived from it, code and link verification with the
same-browser press and the elsewhere-shows-code landing, the server-sent stream the
waiting screen follows, the security step as the policy leaves it, the terms step that
creates the account and signs it in, the identifier operations of a live account with
their undo and their single-address replacement, the profile, the preferences, the
session list, the credential list and its labels, and the three well-known documents are
built. The decisions taken in the owner's absence are in section 4 and, in the same
words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| REG-SESS-001 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.REG_SESS_001_AC1_AbandoningAfterVerifyingLeavesNothingBehindAsync`, `RegistrationFlowTests.REG_SESS_001_AC2_EveryStepIsRefusedWithoutTheFirstContactAsync`, `RegistrationServiceTests.REG_SESS_001_AC3_NoSessionOutlivesTheLifetimeAsync`, `RegistrationServiceTests.REG_SESS_001_AC4_OneEventFiresPerCreatedAccountAsync` |
| REG-SESS-002 | AC1, AC2, AC3 | `RegistrationServiceTests.REG_SESS_002_AC1_NoStepIsReachedOutOfOrderAsync`, `RegistrationServiceTests.REG_SESS_002_AC2_NothingIsWrittenOutsideTheSessionStoreAsync`, `RegistrationServiceTests.REG_SESS_002_AC3_ThePhoneStepIsSkippableOnlyWhereItIsOptionalAsync`, `RegistrationFlowTests.BeginAsync_ABrowserAlreadySignedIn_IsAnsweredWithTheAccountAsync` |
| REG-SESS-003 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.REG_SESS_003_AC1_APressVerifiesAndAPlainOpenDoesNotAsync`, `RegistrationServiceTests.REG_SESS_003_AC2_ElsewhereTheLinkShowsTheCodeAndEndsTheSessionAsync`, `RegistrationServiceTests.REG_SESS_003_AC3_TheSixthWrongCodeEndsTheCodeAsync`, `RegistrationServiceTests.REG_SESS_003_AC4_TheStateCarriesAVerificationMadeByLinkAsync` |
| REG-SESS-004 | AC1, AC2 | `RegistrationServiceTests.REG_SESS_004_AC1_AnUnverifiedExtraHoldsTheStepUntilItIsDroppedAsync`, `RegistrationServiceTests.REG_SESS_004_AC2_ACorrectedNumberIsTheOneSentToAsync` |
| REG-SESS-005 | AC1, AC2, AC3 | `RegistrationServiceTests.REG_SESS_005_AC1_ADuplicateAnswersExactlyAsAFreshAddressDoesAsync`, `RegistrationServiceTests.REG_SESS_005_AC2_TheHoldersNoticeCarriesNoCodeAndNoLinkAsync`, `RegistrationServiceTests.REG_SESS_005_AC3_TheDuplicateSessionExpiresWithoutAnAccountAsync` |
| REG-SESS-006 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.REG_SESS_006_AC1_APasskeyAloneCompletesTheStepAsync`, `RegistrationServiceTests.REG_SESS_006_AC1_APasswordAtTheFloorStandsAloneAndOneBelowItDoesNotAsync`, `RegistrationServiceTests.REG_SESS_006_AC2_AVerifiedEmailStandsAloneOnlyWhereTheFactorIsAdmittedAsync`, `RegistrationServiceTests.REG_SESS_006_AC3_LengtheningThePasswordLiftsTheSecondStepAsync`, `RegistrationServiceTests.REG_SESS_006_AC4_ASecondStepBesideAPasswordDrawsRecoveryCodesAsync` |
| REG-SESS-007 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.REG_SESS_007_AC1_EveryConsentLeftUntickedCompletesTheRegistrationAsync`, `RegistrationServiceTests.REG_SESS_007_AC2_TheAccountCarriesTheVersionsAcceptedAndPresentedAsync`, `RegistrationServiceTests.REG_SESS_007_AC3_APasskeyCarriesTheLevelItSupportsAsync`, `RegistrationServiceTests.REG_SESS_007_AC3_TheSessionCarriesWhatTheMethodsSupportAsync`, `RegistrationServiceTests.REG_SESS_007_AC4_TheRegisteringBrowserIsNotHeldForACodeAsync` |
| REG-SESS-008 | AC1, AC2 | `RegistrationServiceTests.REG_SESS_008_AC1_NoStepTakesADestination`, `RegistrationServiceTests.REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync` |
| REG-PROF-002 | AC1, AC2, AC3, AC4 | `RegistrationServiceTests.REG_PROF_002_AC1_NoIdentifierIsTakenBeforeTheAgeScreenAsync`, `RegistrationServiceTests.REG_PROF_002_AC2_AnUnderAgeDateEndsTheSessionAndLocksTheScreenAsync`, `RegistrationServiceTests.REG_PROF_002_AC3_TheAccountCarriesTheAffirmationAndNotTheDateAsync`, `RegistrationServiceTests.REG_PROF_002_AC3_TheDateIsKeptWhereTheDeploymentKeepsItAsync`, `RegistrationServiceTests.REG_PROF_002_AC4_WithTheAffirmationOffTheBandIsRecordedAsync` |
| REG-PROF-001 | AC2, AC3 (AC1 in phase 0) | `AccountServiceTests.REG_PROF_001_AC2_ASwitchedOffFieldIsNeitherAcceptedNorReturnedAsync`, `AccountServiceTests.REG_PROF_001_AC3_ThePersonDoesNotEditTheDateOfBirthAsync`, `AccountApplicationTests.FE_ACCT_001_AC5_TheGatedProfileFieldsFollowTheirPoliciesAsync` |
| REG-IDENT-001 | AC1, AC2, AC3, AC4, AC5 | `IdentifierServiceTests.REG_IDENT_001_AC1_TheLastVerifiedEmailDoesNotLeaveTheAccountAsync`, `IdentifierServiceTests.REG_IDENT_001_AC2_ANumberOnOneAccountDoesNotVerifyOnAnotherAsync`, `IdentifierServiceTests.REG_IDENT_001_AC3_NoUsernameIsTakenByTheIdentifierPathAsync`, `IdentifierServiceTests.REG_IDENT_001_AC4_TheKeyThatMakesThePhoneOptionalIsALoosening`, `IdentifierServiceTests.REG_IDENT_001_AC5_NoIdentifierIsAddedToANonHumanPrincipalAsync` |
| REG-IDENT-002 | AC3 (AC1, AC2 in phase 1) | `IdentifierServiceTests.REG_IDENT_002_AC3_TheSetAsItWasHearsOfTheBackupChangeAsync` |
| REG-IDENT-004 | AC1, AC2, AC3 | `IdentifierServiceTests.REG_IDENT_004_AC1_AnAdditionWithoutStepUpIsRefusedAsync`, `IdentifierServiceTests.REG_IDENT_004_AC2_AnAddedIdentifierWaitsUnverifiedAsync`, `IdentifierServiceTests.REG_IDENT_004_AC3_TheNoticeSetHearsOfTheAdditionOnceAsync` |
| REG-IDENT-005 | AC1, AC2 | `IdentifierServiceTests.REG_IDENT_005_AC1_ThePrimaryAndTheBackupNeedNoStepUpAsync`, `IdentifierServiceTests.REG_IDENT_005_AC2_AnUnverifiedIdentifierIsNotMadePrimaryAsync` |
| REG-IDENT-006 | AC1, AC2, AC3, AC4 | `IdentifierServiceTests.REG_IDENT_006_AC1_RemovalNeedsStepUpAndSparesThePrimaryAsync`, `IdentifierServiceTests.REG_IDENT_006_AC2_TheUndoRestoresInsideTheWindowAndNotAfterAsync`, `IdentifierServiceTests.REG_IDENT_006_AC3_TheRemovedAddressIsToldWithNoLinkAsync`, `IdentifierServiceTests.REG_IDENT_006_AC4_EveryOtherSessionEndsOnRemovalAsync`, `AccountApplicationTests.FE_ACCT_001_AC3_AnIdentifierIsRemovedAndTheUndoRestoresItAsync` |
| REG-IDENT-007 | AC1, AC2 | `IdentifierServiceTests.REG_IDENT_007_AC1_TheSwapAppliesAndTheUndoGoesToTheOtherChannelAsync`, `IdentifierServiceTests.REG_IDENT_007_AC2_WithNoOtherChannelTheOldAddressConfirmsAsync` |
| REG-IDENT-009 | AC1, AC2, AC3 | `RegistrationServiceTests.REG_IDENT_009_AC1_RegistrationCompletesWithNoUsernameAsync`, `AccountServiceTests.REG_IDENT_009_AC2_ASecondChangeInsideTheWindowIsRefusedAsync`, `AccountServiceTests.REG_IDENT_009_AC3_AnErasedUsernameIsHeldUntilTheRetentionElapsesAsync` |
| REG-IDENT-010 | AC1, AC2 | `RegistrationServiceTests.REG_IDENT_010_AC1_TheConfirmStepOffersNoChangeOnALockedIdentifierAsync`, `RegistrationServiceTests.REG_IDENT_010_AC2_ChangingAnIdentifierResetsItsVerificationAsync` |
| REG-ACCT-001 | AC1 (AC2 in phase 1) | `AccountServiceTests.REG_ACCT_001_AC1_TheReadCarriesEveryGroupThePersonMaySeeAsync`, `AccountFlowTests.REG_ACCT_001_AC1_TheAccountReadsItsOwnGroupsAsync` |
| REG-PREF-001 | AC2, AC3 at the endpoint (both decided in phase 2; AC1 in phase 0) | `AccountApplicationTests.FE_ACCT_001_AC4_AnUndeclaredKeyIsNeitherShownNorTakenAsync`, `PreferenceSetTests.REG_PREF_001_AC2_AnUndeclaredKeyIsRefused`, `PreferenceSetTests.REG_PREF_001_AC3_AnAdministratorOnlyPreferenceIsRefusedFromThePerson` |
| REG-PM-001 | AC2 | `WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`, `WellKnownTests.MapWellKnown_NoDeclaredAddresses_ServesNeitherDocumentAsync` |
| IDN-ATTR-007 | AC2 (AC1 in phase 1) | `AccountApplicationTests.IDN_ATTR_007_AC2_TheSwitchedOffFieldsAreNeitherTakenNorCarriedAsync` |
| IDN-ATTR-008 | AC1, AC2, AC3 | `AccountServiceTests.IDN_ATTR_008_AC1_TheLatestSecondStepIsPreferredWhileNothingIsMarkedAsync`, `AccountServiceTests.IDN_ATTR_008_AC2_AMethodTheAccountDoesNotHoldIsRefusedAsync`, `AccountServiceTests.IDN_ATTR_008_AC3_RemovingThePreferredOneMovesThePreferenceAsync` |
| IDN-ACCT-003 | AC1 | `ApiConventionTests.IDN_ACCT_003_AC1_NoSurfaceTakesTwoAccountsAsync` |
| IDN-LIFE-014 | AC2, AC3 | `SubjectEraserTests.IDN_LIFE_014_AC2_TheTrailStillShowsWhatHappenedAndWhenAsync`, `SubjectEraserTests.IDN_LIFE_014_AC3_TheSubjectIdentifierIsNotReissuedAsync` |
| BFF-CSRF-005a | AC1, AC2, AC3, AC4 | `RegistrationFlowTests.BFF_CSRF_005a_AC1_FirstContactIssuesAPreAuthenticationSessionAsync`, `BrowserProfileTests.BFF_CSRF_005a_AC1_ABrowserHoldingNothingIsGivenAFirstContactAsync`, `RegistrationFlowTests.BFF_CSRF_005a_AC2_TheFirstContactTokenIsValidatedLikeASessionsAsync`, `BrowserProfileTests.BFF_CSRF_005a_AC2_TheFirstContactsTokenIsValidatedLikeASessionsAsync`, `RegistrationFlowTests.BFF_CSRF_005a_AC3_AuthenticationRotatesTheFirstContactAsync`, `RegistrationFlowTests.BFF_CSRF_005a_AC4_AFirstContactCarriesNoIdentityAsync`, `BrowserProfileTests.BFF_CSRF_005a_AC4_AFirstContactCarriesNoIdentityAsync` |
| BFF-CSRF-005b | AC1, AC2, AC3, AC4 | `RegistrationFlowTests.BFF_CSRF_005b_AC1_AnotherBrowserReachesNoneOfTheRegistrationAsync`, `RegistrationFlowTests.BFF_CSRF_005b_AC2_NoTokenTravelsInAUrlAsync`, `RegistrationFlowTests.BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync`, `RegistrationFlowTests.BFF_CSRF_005b_AC4_OnlyAPressFromTheOriginatingBrowserVerifiesAsync` |
| BFF-ERR-001 | AC1, AC2 | `ApiConventionTests.BFF_ERR_001_AC1_NoBodyCarriesASentenceAsync`, `ApiConventionTests.BFF_ERR_001_AC2_TheIdentifierIsTheOneTheRequestIsTracedUnderAsync` |
| BFF-ERR-002 | AC1 | `ApiStatusTests.BFF_ERR_002_AC1_AFaultDisclosesOnlyTheCorrelationIdentifierAsync` |
| BFF-STEP-001 | AC3 | `BrowserProfileTests.BFF_STEP_001_AC3_AnExpiredSessionIsRefusedWithWhatMustBeRedoneAsync` |
| API-CONV-001 | AC1 | `ApiConventionTests.API_CONV_001_AC1_TheHostMountsTheLibraryWhereItLikesAsync` |
| API-CONV-002 | AC1, AC2 | `ApiConventionTests.API_CONV_002_AC1_NoRefusalCarriesASentenceAsync`, `ApiConventionTests.API_CONV_002_AC2_EveryRefusalCarriesItsCorrelationIdentifierAsync` |
| API-CONV-003 | AC1, AC2 | `AccountFlowTests.API_CONV_003_AC1_AConcealedDenialReadsAsAnAbsentRecordAsync`, `ApiStatusTests.API_CONV_003_AC2_OnlyAFailureNamingNoRecordAnswersForbidden`, `ApiStatusTests.Of_ACodeTheLibraryRaises_HasAStatusOfItsOwn`, `ApiStatusTests.Of_EveryCodeButSessionDeath_AnswersWithSomethingOtherThan401` |
| API-CONV-004 | AC1, AC2 | `ApiConventionTests.API_CONV_004_AC1_AStateChangeWithoutTheTokenIsRefusedAsync`, `ApiConventionTests.API_CONV_004_AC2_NoEndpointTakesASessionIdentifier` |
| API-CONV-005 | AC1 | `ApiConventionTests.API_CONV_005_AC1_ADuplicateAddressAnswersAsAFreshOneDoesAsync` |
| AUTH-ABUSE-003 | AC1 (carried from phase 4) | `ApiConventionTests.AUTH_ABUSE_003_AC1_AddingAHeldIdentifierAnswersAsAFreshOneDoesAsync`, `RegistrationServiceTests.REG_SESS_005_AC1_ADuplicateAnswersExactlyAsAFreshAddressDoesAsync` |
| AUTH-FACT-004 | AC2, AC3 (carried from phase 3) | `RegistrationServiceTests.AUTH_FACT_004_AC2_TheVerificationCodeIsTheSessionsAndLivesItsOwnLifetimeAsync`, `RegistrationServiceTests.AUTH_FACT_004_AC3_TheCapKillsTheCodeAndAReplacementReplacesItAsync` |
| AUTH-FACT-016 | AC7 (carried from phase 3) | `RegistrationServiceTests.AUTH_FACT_016_AC7_TheRegisteringBrowserIsSeenForTheLifetimeAsync` |
| AUTH-PASS-001a | AC1 (carried from phase 3) | `RegistrationServiceTests.AUTH_PASS_001a_AC1_AShortPasswordAloneNeverCreatesTheAccountAsync` |
| FE-REG-001 | AC2 | `RegistrationWizardTests.FE_REG_001_AC2_NoStepTakesOrForwardsADestinationAsync` |
| FE-REG-002 | AC1 | `RegistrationWizardTests.FE_REG_002_AC1_APasswordAtTheFloorAndOnNoListIsTakenAsync` |
| FE-REG-003 | AC2, AC3, AC4 | `RegistrationWizardTests.FE_REG_003_AC2_AShortPasswordDoesNotCompleteTheStepAsync`, `RegistrationWizardTests.FE_REG_003_AC3_LengtheningThePasswordLiftsTheSecondStepAsync`, `RegistrationServiceTests.FE_REG_003_AC4_ASecondStepAfterAPasswordLeavesItStandingAsync` |
| FE-REG-004 | AC1, AC2 | `RegistrationWizardTests.FE_REG_004_AC1_ACorrectedAddressIsSentToAtOnceAsync`, `RegistrationWizardTests.FE_REG_004_AC2_TheSameDestinationStaysRestrictedAsync` |
| FE-REG-005 | AC1, AC2, AC3, AC4, AC5 | `RegistrationWizardTests.FE_REG_005_AC1_NoStepIsReachableBeforeItsPredecessorAsync`, `RegistrationWizardTests.FE_REG_005_AC2_OnlyAnUnlockedIdentifierChangesAsync`, `RegistrationWizardTests.FE_REG_005_AC3_ConfirmWaitsForEveryIdentifierAsync`, `RegistrationWizardTests.FE_REG_005_AC4_TheStateIsReadBackFromTheServerAsync`, `RegistrationWizardTests.FE_REG_005_AC5_TheEndLeavesASessionAndNoAddressAsync` |
| FE-VER-001 | AC1, AC2, AC3, AC4 | `RegistrationWizardTests.FE_VER_001_AC1_TheLandingChangesNothingUntilItIsPressedAsync`, `RegistrationWizardTests.FE_VER_001_AC2_ThePressAdvancesWhatTheWaitingScreenReadsAsync`, `RegistrationWizardTests.FE_VER_001_AC3_ElsewhereShowsTheCodeAndEndsTheAttemptAsync`, `RegistrationWizardTests.FE_VER_001_AC4_AnAccountsIdentifierLinkBehavesTheSameAsync` |
| FE-ACCT-001 | AC1, AC2, AC3, AC4, AC5 | `AccountApplicationTests.FE_ACCT_001_AC1_ACredentialShowsItsPropertiesAndOnlyItsLabelChangesAsync`, `AccountApplicationTests.FE_ACCT_001_AC2_OneSessionIsCurrentAndAnotherIsEndedAsync`, `AccountApplicationTests.FE_ACCT_001_AC3_AnIdentifierIsRemovedAndTheUndoRestoresItAsync`, `AccountApplicationTests.FE_ACCT_001_AC4_AnUndeclaredKeyIsNeitherShownNorTakenAsync`, `AccountApplicationTests.FE_ACCT_001_AC5_TheGatedProfileFieldsFollowTheirPoliciesAsync` |
| LIB-API-001, CONV-NAME-003 | AC2, for the vocabularies this phase adds | `VocabularyContractTests.WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten`, `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest` |

The endpoints of `09` sections 2 and 6 the plan's Builds column names are mounted:
the registration group with its stream and its abandon path, the account group with
identifiers, credentials and labels, profile, preferences and sessions, and the three
documents of REG-PM-001 and AUTH-FACT-012 at the site root. Section 4, decision 54,
states which endpoints of section 6 belong to later phases and why.

Criteria a test cannot decide, named here as CONV-TEST-007 requires:

| Criterion | How it was verified |
|---|---|
| API-CONV-005 AC2 and AUTH-ABUSE-003 AC2, that response timing does not vary measurably with existence | By construction, as the criteria themselves direct. One code path answers both cases: the staged identifier is drawn before the directory is asked, the owner lookup is a keyed fingerprint comparison whichever answer it gives, and the notice to an existing holder is sent on the same path as the code to a fresh address. Asserted by the byte identity of criterion 1 (`ApiConventionTests.API_CONV_005_AC1_ADuplicateAddressAnswersAsAFreshOneDoesAsync`, `ApiConventionTests.AUTH_ABUSE_003_AC1_AddingAHeldIdentifierAnswersAsAFreshOneDoesAsync`) |
| AUTH-ABUSE-003 AC6, the wording of the notice an existing holder receives | Host template content under CONV-CONTENT-001. What the library decides is that the notice carries no code and no link and names nobody, which `RegistrationServiceTests.REG_SESS_005_AC2_TheHoldersNoticeCarriesNoCodeAndNoLinkAsync` decides |
| FE-REG-001 AC1, FE-REG-003 AC1, FE-VER-001 AC5, FE-PM-001 to FE-PM-006, FE-SEC-001 | The frontend's, as the plan's Chapters column states ("server side only; screens are the frontend's"). The server halves are built and tested here: no step takes or forwards a destination (`RegistrationWizardTests.FE_REG_001_AC2_NoStepTakesOrForwardsADestinationAsync`), the floors the security screen states (`RegistrationWizardTests.FE_REG_003_AC2_AShortPasswordDoesNotCompleteTheStepAsync`), no route of the flow names a token (`RegistrationFlowTests.BFF_CSRF_005b_AC2_NoTokenTravelsInAUrlAsync`), and the two documents FE-PM-005 follows (`WellKnownTests.REG_PM_001_AC2_TheWellKnownDocumentsAnswerAndTheProbeDoesNotAsync`) |
| BFF-CSRF-003 AC2 | Settled in phase 3 and unchanged: a state change without the custom request header is refused whatever else it carries |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| BFF-ERR-001 AC3 | Six codes the library raises have no row in `10` section 1, so the criterion cannot pass as written. Each is a refusal a chapter describes in prose, and each is documented in the library's own catalogue | Section 4, decision 40; `10` sections 1.1 and 1.2 |
| REG-ACCT-001 AC3 | Erasure over the account table's field groups is the erasure and export work | Phase 7, PRIV-RIGHT-003 and PRIV-RIGHT-005 |
| REG-PREF-001 AC4 | The export of PRIV-RIGHT-003 carrying the preferences | Phase 7 |
| REG-IDENT-003 AC1, AC2 | `POST /auth/begin` takes the one identifier field, detects its kind and conceals existence; no sign-in endpoint exists yet | Phase 6, `09` section 3 |
| REG-IDENT-007 AC3 | The swap inside an enrolment session opened for a lost mailbox is the admin-assisted recovery path | Phase 6, AUTH-RECOV-002 |
| REG-IDENT-008 AC1 to AC4 | A Google or Apple identity as a credential needs the social consumer, which no phase of Milestone 1 builds and which the library holds none of | Phase 6, `09` section 4 |
| REG-PM-001 AC1 | No user handle contains personal data, proved where the user handle is written, which is the WebAuthn registration ceremony | Phase 6; section 4, decision 52 |
| REG-DOM-001, REG-INV-001, REG-INV-002 | Invitations and the domain lock are their own surface, with the staff onboarding they belong to | Phase 8 |
| REG-MAIL-001, REG-MAIL-002, REG-MAIL-003 | The corporate mailbox is provisioned against the mail server the plan schedules separately | Phase 8, INT-MAIL |
| API-CONV-001 AC2 | The discovery document reflecting the prefix is the OIDC provider's document | Phase 6 |
| API-REDIR-001, API-REDIR-002 | No endpoint of this phase takes a return address, and registration is exempt from the rules | Phase 6; section 4, decision 53 |
| API-LAND-001 AC1 to AC3 | The landing route is the frontend's page; what the library owes it is the code the link resolves to, decided through REG-SESS-003 | Phase 6 with the sign-in links, and the frontend |
| IDN-ATTR-008 AC4 | The preferred method is offered first where a challenge is presented, and no endpoint of this phase presents one | Phase 6; section 4, decision 56 |
| IDN-ATTR-002, IDN-ATTR-004, and the photo endpoints of `09` section 6 | The upload has to be recognised by content and re-encoded to JPEG with its metadata removed, and no permitted package decodes an image | Section 4, decision 55 (Tier 3); CONV-DESIGN-008 |
| AUTH-STEP-007 AC1, AUTH-FACT-002a AC5 | Both need an enrolment on a live account, which is the factor surface of `09` section 3, and the second names a provider link the library holds no consumer for | Phase 6; section 4, decision 57 |
| `DELETE /account/credentials/{id}`, `/account/credentials/{id}/upgrade`, `POST /account/link/{provider}`, `POST /account/password`, the TOTP and recovery-code enrolment endpoints, `/account/language`, deactivate, delete and the mail app passwords | Endpoints of `09` section 6 whose operations other phases build | Phases 6 to 8; section 4, decision 54 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `IdentifierService.SwapAsync`, `IIdentifierDirectory` | The single-address replace gave up the identifier it was replacing, which is the primary of that kind, and a primary is never removed | REG-IDENT-007 with REG-IDENT-002 | The change is one operation on the identifier that stands: the directory gained `ReplaceAsync`, which records the displaced value for the undo and puts the new value on the same row |
| `RegistrationJson`, `AccountJson` | The generated contexts named no string enum converter, so `step`, `kind` and `state` crossed the boundary as numbers and the stream threw on every change | `09` section 2, which gives the shapes (`"step": "email"`, `"kind": "email"`) | The contexts generate with `UseStringEnumConverter`, which makes the names of `10` section 5.19 the ones the wire carries |
| `RegistrationEndpoints.TermsAsync` | The first contact's cookie was cleared and its row left to expire on its own | BFF-CSRF-005a AC3 | The terms step rotates the first contact it replaces, which is what "rather than issuing a second session alongside" asks of the session that takes its place |
| `src/Janus.Hosting/Bff/Refusal.cs` | The `Retry-After` a 429 carries was computed from the ambient clock | `08` section 1a, "Time comes from `TimeProvider`" | The only clock the library reads is the registered provider, so the writer takes it from the request's services like every other reader of time |
| `src/Janus.Hosting/Bff/ApiStatus.cs` | `auth.restriction.exceeded` was mapped to 422 | `09` sections 2 and 3, which give it 429 at every endpoint that names it, and `10` section 6, which reserves 429 for an answer carrying `Retry-After` | The status is a property of the code, and the code the chapters name with 429 answers 429 |
| `.github/gates/forbidden-markers.sh` | The job read any comment line ending in a semicolon or a brace as commented-out code, and prose ends a clause in a semicolon too | CONV-CODE-005 AC2, the working guide's section 3, a gate that mis-implements its own rule | The job requires the terminated comment to carry what only code carries, a call, an assignment, an index, a brace, a member access or a statement keyword at its head, so it flags commented-out code and not a sentence |

## 4. Decided in the owner's absence

Under D-161. Each entry is in `docs/reports/decisions-pending-review.md` in the same
words, numbered as it is there.

| # | Decision |
|---|---|
| 35 | A given-up identifier stays reserved for the whole undo window |
| 36 | The username hold after erasure is its own table |
| 37 | A pending identifier verification reuses the staged identity of registration |
| 38 | The preferred second step is stored as a mark and the order derived |
| 39 | Every error code is mapped to one status in one table |
| 40 | Six codes the chapters describe but do not name |
| 41 | Session resolution refuses a dead session in the pipeline |
| 42 | A session records no location until a local database can resolve one |
| 43 | The browser and the operating system are read from the user agent |
| 44 | The phone step is skipped through a path of its own |
| 45 | The second-step choice names the next ceremony and is recorded nowhere |
| 46 | A signed-in browser that asks to register is answered with the account |
| 47 | Every event of the stream carries the state document |
| 48 | The stream is produced by reading the state back on an interval |
| 49 | A request the reader cannot parse is answered 400 with no body |
| 50 | The addresses of the frontend's passkey pages are declared by the host |
| 51 | The last-of-kind refusal is unreachable where the primary cannot be removed |
| 52 | The WebAuthn registration ceremony is carried to phase 6 with the rest of `09` section 3 |
| 53 | API-REDIR-001 has no endpoint in phase 5 to govern |
| 54 | The phase's surface is the plan's Builds column, not the whole of `09` section 6 |
| 55 | The photo endpoints cannot be built inside the permitted packages |
| 56 | The preferred second step is presented by the challenge that phase 6 builds |
| 57 | Two criteria wait on an enrolment that only a live account can have |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged. The local counts at the end of the phase:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 416, `Janus.Authorization.Tests`
96, `Janus.Core.Tests` 413, `Janus.Hosting.Tests` 201, `Janus.Identity.Tests` 62,
`Janus.Privacy.Tests` 13 and `Janus.Storage.Tests` 200, none failing.

Full gate: GitHub Actions runs `35492440489` (push) and `35492441499` (pull request) on
branch `phase-05-registration`, pull request #13, green on every job. `Integration
tests`, `Double migration run`, `Destructive-operation detection report`, `Truth-table
suite` and `Dependency vulnerability alerting` run on the pull-request event and `Secret
scanning` on the push event, as CONV-GATE-002 states, so the two runs together are one
pass of the table of CONV-GATE-001.

The commit after the two runs above changes this section alone.
