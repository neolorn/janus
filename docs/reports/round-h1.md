# Round H1: the host's way in, its privacy duties, its setup and its tests

## 1. Items implemented

Criteria proven by tests that existed before the round, and still pass, are marked "at
base".

**`17` section 2a, the host's way in**

- BFF-HOST-001:
  - AC1: `RequestAccessTests.BFF_HOST_001_AC1_TheAccessIsWhatTheLibrarysEndpointsUseAsync`.
  - AC2: `..._AC2_NoAccessIsAnsweredWithoutALiveSignInSessionAsync`.
  - AC3: `..._AC3_ABreakGlassSessionsReasonReachesWhatAHostsCallWritesAsync`.
  - AC4: `PublicSurfaceTests.BFF_HOST_001_AC4_NoPublicMemberMakesAContextWhoseIdentitiesDiffer`.
  - AC6: `RequestAccessGateTests.BFF_HOST_001_AC6_ASessionDowngradedSinceAal2DoesNotMeetTheGateFromAHostEndpointAsync`.
  - Also: `PublicSurfaceTests.BFF_HOST_001_TheAccessIsRegisteredInEachRequestScopeAndCarriesTheContextAndTheSessionAlone`.
- BFF-HOST-002, in `HostEndpointTests` unless marked:
  - AC1: `BFF_HOST_002_AC1_AnUnmarkedHostEndpointRefusesABrowserHoldingNoSessionAsync` and
    `..._AnUnmarkedHostEndpointAnswersAnEndedSessionWithWhatMustBeRedoneAsync`.
  - AC2: `BFF_HOST_002_AC2_AMarkedHostEndpointAnswersAnyoneAndIsHeldToTheTokenAsync`.
  - AC3: `RequestAccessGateTests.BFF_HOST_002_AC3_AnUnmetGateIsAnsweredWithItsDetailsAsync`.
  - AC4: `BFF_HOST_002_AC4_TheLibrarysEndpointsAnswerARequestWithNoSessionAsBeforeAsync`.
- BFF-HOST-003:
  - AC1: `RequestAccessTests.BFF_HOST_003_AC1_AFailureIsTheLibrarysOwnAnswerAsync`.
  - AC2: `RequestAccessGateTests.BFF_HOST_003_AC2_AConcealedRefusalIsAnsweredAsTheAbsenceWhateverTheEndpointWroteAsync`.
  - AC3: `RequestAccessTests.BFF_HOST_003_AC3_ACodeTheReferenceDoesNotListIsAFaultAsync`.
  - Also: `PublicSurfaceTests.BFF_HOST_003_AHostEndpointIsGivenTheMembersTheChapterNamesAndNoOthers`.
- BFF-HOST-004 AC1: `RequestAccessTests.BFF_HOST_004_AC1_TheSourceIsTheOneTheLibrarysEndpointsPass`.
- BFF-OWN-001 AC1: `BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration` (adapted to the host's endpoints).
- BFF-OWN-002 AC2: `BrowserProfileTests.BFF_OWN_002_AC2_NoHostCodeSetsTheSessionCookieOrValidatesTheToken` (now also scans `HostEndpoints.cs`).
- BFF-CAP-001 AC1, at base: `BFF_CAP_001_AC1_AListOfFiftyCarriesItsCapabilitiesInOneStatementAsync`.
- BFF-CAP-002:
  - AC1: by construction. The fixture declares its capabilities through the builder
    alone, and the library has no change for it.
  - AC2: `GateBehaviourTests.BFF_CAP_002_AC2_ADeclaredPermissionTheTypeDoesNotListAsACapabilityRaisesAsync`,
    with the truth-table row "a page asked for a declared permission its type does not
    list as a capability" (Raised).
  - AC4: `AuthorizationModelTests.BFF_CAP_002_AC4_ACapabilityTheModelDoesNotDeclareFailsStartup`.
- BFF-CAP-003, at base: `BFF_CAP_003_AC1_AListOfFiftyCostsTheStatementsAListOfOneCostsAsync`.
- BFF-ORDER-001 stage 8, AC4: `BFF_ORDER_001_AC4_AnEndedSessionReachesAMarkedHostEndpointAndIsRefusedByAnUnmarkedOneAsync`.
- BFF-SESS-002 AC3:
  - `SessionServiceTests.BFF_SESS_002_AC3_UnderAnAal1PolicyThePairLastsUntilTheAbsoluteExpiryAsync`,
    `..._UnderAnAal2PolicyThePairEndsWithTheBrowserAsync` and
    `..._APairIssuedAgainFollowsThePolicyThenInForceAsync`;
  - `BrowserCookieTests.BFF_SESS_002_AC3_ThePairLastsUntilTheInstantTheIssueStatesAndOtherwiseEndsWithTheBrowser`;
  - `SessionCookieLifetimeTests.BFF_SESS_002_AC3_ASessionsPairLastsAndThePairBeforeItDoesNotAsync`.
- BFF-CSRF-005a AC1: `BFF_CSRF_005a_AC1_FirstContactWithAMarkedHostEndpointIssuesAPreAuthenticationSessionAsync`.

**`07` the library contract**

- LIB-PKG-001:
  - AC1, at base: `LIB_PKG_001_AC1_NoAreaIsCompiledAgainstAnotherAreasInternals`.
  - AC2: `LIB_PKG_001_AC2_NoAreaDependsOnAnotherArea` (now with the `Janus.Testing` row).
  - AC4:
    - `PackageTests.LIB_PKG_001_AC4_TheMainPackageCarriesTheSevenAssembliesAndTheFramework`;
    - `..._TheMainPackageDeclaresThePackagesItsProjectsReferenceAtRunTime`;
    - `..._TheCompanionsDependOnTheMainPackageAndNoneOnTheAnalysers` (the conformance
      suite and the testing package).
- LIB-API-001, the rows this round adds:
  - the host's access, answers and source;
  - `IExportContributor`;
  - the testing package's built members.
  These are proven by `PublicSurfaceTests.LIB_API_002_AC1_InternalTypesAreNotPubliclyAccessible`
  and the `PublicAPI.Unshipped.txt` files of `Janus.Core`, `Janus.Hosting` and
  `Janus.Testing`.
- LIB-API-002:
  - AC1: `PublicSurfaceTests.LIB_API_002_AC1_InternalTypesAreNotPubliclyAccessible`
    (`Janus.Testing` among the shipped assemblies).
  - AC2, the conformance sample's schema:
    - `LibraryStructureTests.LIB_API_002_AC2_TheConformanceSampleTakesItsSchemaFromMigrate`;
    - `LIB_API_002_AC2_NoProjectOpensItsInternalsToTheSampleHost`;
    - the conformance suite, 40 of 40, on a schema applied by `migrate`.
- LIB-API-005, at base: `IdentityEndpointsTests` and the other existing tests. The round
  changes the body only.
- LIB-HOST-001, the rows built:
  - capabilities (BFF-CAP-002);
  - subject-event handlers, takedown only (PRIV-RIGHT-005b);
  - purpose handlers (PRIV-RIGHT-001a);
  - export contributors (PRIV-RIGHT-003);
  - cookies (PRIV-CONS-006a);
  - the secret source as `migrate` reads it (OPS-MIG-001).
  Each row is proven by the tests of the item named.
- LIB-HOST-004, at base: `AccessSeamTests` and `StepUpGatesTests`.
- LIB-EXT-001 AC1 to AC4, at base: `IntegrationBoundaryTests`. `FixedSecretSource` is
  proven in LIB-TEST-003.
- LIB-TEST-002 AC2, at base: `SchemaContractTests`.
- LIB-TEST-003, the members no question holds:
  - AC2: `SurfaceTests.LIB_TEST_003_AC2_NoMemberCanWriteALibraryTableOrCallAnInternalType`.
  - AC4: `Alerting.StandInTests.LIB_TEST_003_AC4_TheStandInsRaiseNoDegradationAsync`.
  - The transports:
    - `LIB_TEST_003_TheMailTransportKeepsEveryMailByDestinationAsync`;
    - `LIB_TEST_003_TheMailTransportFailsADeliveryOnlyWhereTheTestSaysSoAsync`;
    - `LIB_TEST_003_TheSmsTransportKeepsEveryMessageByDestinationAsync`.
  - `FixedSecretSource`:
    - `LIB_TEST_003_EachInstanceDrawsKeysOfItsOwnAsync`;
    - `LIB_TEST_003_TheSecretsTheTestGivesAreAnsweredAndNoOthersAsync`;
    - `LIB_TEST_003_TheKeyDocumentCarriesTheConnectionAndTheSameKeysAsync`.
  - The seam stand-ins:
    - `LIB_TEST_003_TheClockIsOnTimeAndTheRenewalSucceededAsync`;
    - `LIB_TEST_003_TheRangeFilesHoldOnlyTheirDateOfTheDayReadAsync`.
  - `SoftwarePasskey`: `LIB_TEST_003_ASoftwarePasskeyEnrolsAndSignsInThroughTheLibrarysCeremoniesAsync`.

**`03` authorization**

- AUTHZ-IMP-001:
  - AC6: `AccessSeamTests.AUTHZ_IMP_001_AC6_NoPublicMemberMakesAContextWhoseIdentitiesDiffer`.
  - AC5 cases adapted to contexts the public surface can make, in:
    - `AccountAdministrationTests`, `AccountLifecycleTests`, `CredentialServiceTests`;
    - `PasswordServiceTests`, `RestrictionAdministrationTests`, `AlertDestinationChangeTests`;
    - `LossReportsTests`, `TakedownServiceTests`.
- AUTHZ-GATE-005 AC1, at base:
  - `AUTHZ_GATE_005_AC1_APageIsOneStatementNamingEveryRecordByParameter`;
  - `..._APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`;
  - `..._APageCostsOneStatementOverTheHostsRowsAsync`.
- API-CAP-001 AC1, at base: `API_CAP_001_AC1_AListOfFiftyCarriesItsCapabilitiesInOneStatementAsync`.

**`01` identity**

- IDN-MEM-001 AC6: `InvitationServiceTests.IDN_MEM_001_AC6_TheReservedAccountsMembershipOfTheAdministrativeOrganizationIsNotEndedAsync`.
- IDN-LIFE-003a:
  - AC7:
    - `APurposeHandlerUnderATakenNameFailsStartup`;
    - `APurposeHandlerUnderASubscribersNameIsRefusedAsync`;
    - `IDN_LIFE_003a_TheDeliveryChangesAreTheContract`.
  - AC9:
    - `EveryIncompleteDeliveryOfEveryKindIsListedAsync`;
    - `ADeliveryOfAnyKindIsReadByItsIdentifierAsync`;
    - `AFailedConsentOrObjectionDeliveryIsCompletedByHandAsync`;
    - `AConsentDeliveryThatNeverFailedIsNotCompletedAsync`;
    - `EveryIncompleteDeliveryOfEveryKindIsReadInOneQueryAsync`;
    - `EveryKindIsListedWithItsKindPurposeAndChangeAsync`;
    - `IDN_LIFE_003a_TheDeliveryKindsAreTheContract`;
    - `IDN_LIFE_003a_AnIdentifierNamingNoDeliveryIsNotFoundAsync`.

**`02` authentication**

- AUTH-KEY-002 AC1 to AC4, at base: `KeyMaterialTests`. `migrate` reading no key is
  proven by `MigrateDocumentTests.OPS_MIG_001_AC4_TheCommandRegistersNoKeyRingAsync`.

**`04` privacy**

- PRIV-RIGHT-005a:
  - AC20, in `PersonalFieldCipherTests`:
    - `PRIV_RIGHT_005a_AC20_AHostValueIsBoundToTheMarkTheTypeAndTheFieldAsLaidOut`;
    - `..._AHostValueMovedElsewhereDoesNotDecrypt`;
    - `..._AHostValueAndALibraryValueNeverStandForEachOther`.
  - AC20, in `PersonalFieldsTests`:
    - `..._AHostFieldWrittenThroughTheCipherReadsBackUnderItsSubjectAsync`;
    - `..._AMemberGivenItsOldNameReadsWhatWasWrittenUnderItAsync`;
    - `..._AValueMovedToAnotherSubjectDoesNotDecryptAsync`;
    - `..._AnAlteredValueIsAFaultAsync`;
    - `..._AfterErasureAFieldAnswersSubjectErasedAsync`;
    - `..._APageIsReadWithOneStatementAndOneUnwrapPerSubjectAsync`;
    - `..._AFieldItsTypeDoesNotDeclareEncryptedIsAFaultAsync`;
    - `..._AViewUsedAfterTheUseReturnedIsAFaultAsync`;
    - `..._AUseThatThrowsStillReturnsTheKeyAsync`.
  - AC20, also: `LentFieldsTests.PRIV_RIGHT_005a_AC20_TheKeyIsClearedWhenTheLoanEnds`.
  - AC21:
    - `PRIV_RIGHT_005a_AC21_EncryptingForAnErasedSubjectIsRefusedAsync`;
    - `PRIV_RIGHT_005a_AC21_ASubjectWithNoKeyIsRefusedNamingSubjectAsync`.
  - Truth-table row "a grant on a record whose type declares its encrypted field under a
    name of its own" (Allowed).
- PRIV-RIGHT-005b:
  - `SubjectEventContractTests.PRIV_RIGHT_005b_NoEventIsOwedAfterAnExport`;
  - `ExportEndpointTests.PRIV_RIGHT_005b_AnAssembledExportOwesTheSubscribersNothingAsync`.
- PRIV-RIGHT-003:
  - AC5, in `ExportServiceTests`:
    - `PRIV_RIGHT_003_AC5_EachContributorsSectionsFollowTheLibrarysInNameOrderAsync`;
    - `..._AFailingContributorFailsTheExportUncountedAsync`;
    - `..._AFaultingContributorFailsTheExportUncountedAsync`;
    - `..._ASectionUnderANameAlreadyTakenFailsTheExportAsync`;
    - `..._AnExportCountedWhileTheContributorsAnsweredIsCountedAsync`.
  - AC5, in `ExportEndpointTests`:
    - `..._TheReadableFormatCarriesTheContributorsSectionsAfterTheLibrarysAsync`;
    - `..._AFaultingContributorAnswersAFaultAndIsNotCountedAsync`.
  - AC6:
    - `HandlerCoverageTests.PRIV_RIGHT_003_AC6_ASensitiveTypeNoContributorCoversFailsStartup`;
    - `..._ATypeWithAnEncryptedFieldNoContributorCoversFailsStartup`;
    - `StartupValidationTests.PRIV_RIGHT_003_AC6_ADeploymentWithNoContributorForItsPersonalTypeIsRefusedAsync`.
  - The lock under concurrent exports is `IExportLedger.HoldAsync`'s (`ExportLedgerTests`).
- PRIV-RIGHT-001a AC3:
  - `AConsentChangeReachesEveryHandlerNamingItsPurposeAsync`;
  - `AnObjectionChangeReachesEveryHandlerNamingItsPurposeAsync`;
  - `AHandlerThatDoesNotConfirmIsRetriedUntilTheAttemptsAreSpentAsync`;
  - `AnObjectionChangeIsOwedToItsPurposesHandlersAsync`;
  - `AConsentBasedPurposeWithNoRegisteredHandlerFailsStartup`;
  - `HandlersTogetherNamingEveryPurposeStart`;
  - `APurposeOnAnotherBasisNeedsNoHandler`;
  - `AConsentBasedPurposeWithoutAHandlerStopsStartupAsync`;
  - `AConsentAndAnObjectionChangeKeepTheirPurposeAndChangeAsync`;
  - `PRIV_RIGHT_001a_ADeliveryNamingThePurposeOrChangeOfAnotherFactIsRefusedAsync`.
- PRIV-CONS-006a:
  - AC1:
    - `ANoticeVersionKeepsTheCookiesSetWhenItWasPublishedAsync`;
    - `AVersionKeepsTheCookiesItListsAsync`;
    - `TheLibrarysCookiesAndEveryDeclaredOneAreListedAsync`;
    - `TheNoticeAnswersTheCookiesItsVersionListsAsync`;
    - `PRIV_CONS_006a_TheLibrarySetsOnlyTheFiveNecessaryCookies`.
  - AC3: `ACookieDeclaredOutsideItsRuleIsRefusedAsync` (eight cases) and
    `CookiesDeclaredWithinTheirRuleStartAsync`.
  - AC4:
    - `NothingIsRaisedBeforeAFirstVersionIsPublishedAsync`;
    - `CookiesTheCurrentVersionListsRaiseNothingAsync`;
    - `CookiesThatDifferRaiseADegradationNamingThemAsync`;
    - `APurposeOrAThirdPartyReadingThatDiffersIsAChangeAsync`;
    - `ATrustedDeviceLifetimeTheNoticeDoesNotListRaisesADegradationAsync`;
    - `AStartWhoseCookiesTheNoticeDoesNotListRaisesADegradationAsync`.
  - AC2 is frontend behaviour; no server test decides it.
- PRIV-CONS-008 AC4:
  - `PRIV_CONS_008_AC4_AConsentChangeIsOwedToItsPurposesHandlersAsync`;
  - `PRIV_CONS_008_ASupersededConsentIsOwedToItsPurposesHandlersAsync`.

**`05` integrations**

- INT-SMS-003 AC3, the handler part:
  - `OutstandingIsTheWiderOfTheSubscribersAndTheHandlers`;
  - `APurposeHandlerNamedOutsideTheRuleIsRefusedAsync`.
- INT-PWD-001 AC3: `ScreeningTests.INT_PWD_001_AC3_EveryRangeRequestNamesTheLibraryAndItsVersionAsync`.
  It reads the main package's identifier from its build.

**`06` operations**

- OPS-BOOT-001 AC8: `BootstrapTests.OPS_BOOT_001_AC8_StandardOutputHoldsTheLinkThenTheOrganizationsIdentifierAsync`.
- OPS-BOOT-002 AC7, the membership part: `InvitationEndpointTests.OPS_BOOT_002_AC7_TheReservedAccountsMembershipOfTheAdministrativeOrganizationIsKeptAsync`.
- OPS-MIG-001:
  - AC2: `MigrateTests.OPS_MIG_001_AC2_ARefusedMigrationFailsTheRunAndNothingAfterItIsAppliedAsync`.
    The double-migration gate also applies through `migrate`.
  - AC3: `MigrateTests.OPS_MIG_001_AC3_ASecondRunAppliesNothingAndChangesNothingAsync`.
  - AC4:
    - `MigrateDocumentTests.OPS_MIG_001_AC4_ADocumentCarryingAnotherMemberIsRefusedAsMalformedAsync`;
    - `..._TheCommandRegistersNoKeyRingAsync`;
    - `MigrateTests.OPS_MIG_001_AC4_ADocumentCarryingAKeyAppliesNothingAsync`;
    - `MigrateDocumentTests.OPS_MIG_001_AC4_ABlankOrNonStringConnectionIsRefusedAsMalformedAsync`.
  - Also: `OPS_MIG_001_AnArgumentIsRefusedByNameAsync`.
- OPS-SEC-001 AC4, for `migrate`:
  - `OPS_SEC_001_AC4_MigrateRefusesATerminalAsync`;
  - `..._MigrateRefusesADocumentCarryingNoConnectionAsync`;
  - `..._MigrateRefusesADocumentThatDoesNotReadAsMalformedAsync`.
- OPS-ENV-001 AC1: `BootstrapTests.OPS_ENV_001_AC1_AFreshDatabaseYieldsAUsablePermissionRealisticDatasetAsync`.
  It runs `migrate`, then `bootstrap`, then `register-client` on an empty database.
- OPS-ALERT-001:
  - the purpose handler that does not confirm, raised as the subject-event subscriber is
    (`AHandlerThatDoesNotConfirmIsRetriedUntilTheAttemptsAreSpentAsync`);
  - `notice.cookies` (PRIV-CONS-006a AC4 above).

**`08` engineering conventions**

- CONV-LAYOUT-001:
  - AC2, at base: `CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`.
  - AC3: `CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives` (the
    `Janus.Testing` row).
  - AC4: `CONV_LAYOUT_001_AC4_EveryProjectTakingTheSharedFrameworkCarriesIt`.
  - AC5: `PackageTests.CONV_LAYOUT_001_AC5_OnlyThePackableProjectsPack` (five packages)
    and `..._TheMainPackagesDependenciesAreItsProjectsRunTimePackages`.
- CONV-LAYOUT-002 AC1: `CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay`.
  `Janus.Testing` grants nothing and is granted nothing.
- CONV-SETUP-002 AC1: `CONV_SETUP_002_AC1_NoProjectFileCarriesAPackageVersion`. It now
  covers `tests` and `samples` beside `src` and `tools`.
- CONV-SETUP-003 AC1: verified by the build. `Janus.Testing` carries both `PublicAPI`
  files, and the analyser stops a public member without its line.
- CONV-SETUP-004 AC3: `CONV_SETUP_004_AC3_NoRuleButTheFiveOfTheTableIsSetBelowError`
  (CA1515 scope with `Janus.Testing`).
- CONV-DESIGN-003: the pipeline applies migrations by `migrate`. `.github/gates/double-migration.sh`
  does, and no gate runs `dotnet ef database update`.
- CONV-DESIGN-007: `OPS_MIG_001_AC4_TheCommandRegistersNoKeyRingAsync`.
- CONV-DESIGN-008:
  - AC1, at base: `CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList`.
  - AC3: `CONV_DESIGN_008_AC3_TheRelationalAccessPackagesAndTheirToolCarryOneVersion`
    (`Janus.Hosting` beside `Janus.Storage`).
- CONV-CODE-004, at base: `LibraryIdentityTests`.
- CONV-TEST-001:
  - AC2: `CONV_TEST_001_AC2_NoProjectGrantsItsInternalsToTheProjectHoldingTheSample`.
  - `tests/Janus.Testing.Tests` exists, and the test projects reference directly the
    projects the main package holds privately.
- CONV-DEP-001: lockfiles for `src/Janus.Testing` and `tests/Janus.Testing.Tests`.

**`09`, `10` and `18`**

- `09`:
  - section 14 (wording);
  - `POST /auth/step-up`, at base: `StepUpGatesTests`;
  - `GET /privacy/notice` (PRIV-CONS-006a);
  - `GET /privacy/export` (PRIV-RIGHT-003);
  - `/admin/erasures` and `POST /admin/erasures/{id}/complete` (IDN-LIFE-003a AC9);
  - `DELETE /admin/organizations/{id}/memberships/{subject}` (OPS-BOOT-002 AC7).
- `10`:
  - section 1: `privacy.subject.erased`, `privacy.erasure.notfound` and
    `privacy.erasure.notfailed`, through the items above;
  - section 5.28: `role-seeding` (D-206 below), and no operation for `migrate` (Q17);
  - sections 5a and 5b: the delivery kinds and changes, through IDN-LIFE-003a;
  - REF-001 AC3 and AC4, at base.
- FE-COMP-006, the server side:
  - criterion 1b: `TheNoticeAnswersTheCookiesItsVersionListsAsync`;
  - criterion 3: `ExportEndpointTests.PRIV_RIGHT_003_AC5_TheReadableFormatCarriesTheContributorsSectionsAfterTheLibrarysAsync`.

**Built under D-206**

- Q16, the held commits: `78ae1925` was amended to meet CONV-VCS-003, with `bd1b5da0`
  after it (now `89db2937` and `78fb2cbc`), and `part/authz-host` was merged (`727e76d7`).
  - AUTHZ-INHERIT-002 AC5: `ResourceRegistrationTests.AUTHZ_INHERIT_002_AC5_ARemovedRegistrationLeavesNoAncestryAndNoLiveGrantAsync`
    (the revocation names the holder as its subject).
  - AUTHZ-INHERIT-003 AC3: `AncestryStoreTests.AUTHZ_INHERIT_003_AC3_ARemovalLeavesNoAncestryRowOrphanedAsync`.
  - Truth-table row "a grant on a record whose registration was removed, the record
    registered again" (Denied).
  - AUTHZ-GRANT-004 AC7:
    - `AuthorizationModelTests.AUTHZ_GRANT_004_AC7_ADeclaredRoleNamedAsAnAdministrativeRoleFailsStartup`;
    - `AuthorizationModelTests.AUTHZ_GRANT_004_ADeclaredRoleOutsideItsRuleFailsStartup`;
    - `AuthorizationModelTests.AUTHZ_GRANT_004_TheModelKeepsTheDeclaredRolesInOrder`;
    - `StartupValidationTests.AUTHZ_GRANT_004_AC7_ADeclaredRoleIsSeededOnceAndThenLeftToTheAdministratorsAsync`;
    - `RoleEndpointTests.AUTHZ_GRANT_004_AC7_AnIRolesCallWithASystemPrincipalsContextIsRefusedAsync`;
    - truth-table row "a role defined with the context of the start principal
      role-declaration" (Denied).
  - AUTHZ-MODEL-004 AC2: `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`.
    The merge dropped the cookies check from the test's expected order; `88a42394`
    restored it.
  - `POST /admin/roles`, `10` section 1 `authz.resource.inuse`, and section 5.28
    `role-seeding` and `role-declaration`, through the tests above and
    `ErrorCodesTests.REF_001_AC1_EveryLiveRowOfTheReferenceIsACodeInTheSource`.
- Known issue 1 (`dd625f63`), AUTHZ-GRANT-003 AC6:
  - `GrantAuditTests.AUTHZ_GRANT_003_AC6_AGrantToAnAccountIsRecordedCreatedNamingItsHolderAsync`,
    `..._AGrantToAGroupIsRecordedWithNoSubjectAsync`,
    `..._AGrantBootstrapWritesIsRecordedUnderItsPrincipalAsync` and
    `..._ARevocationIsRecordedNamingTheHolderWhoeverRevokedItAsync`;
  - `GrantEndpointTests.AUTHZ_GRANT_003_AC6_APersonsGrantAndRevocationAreEachRecordedAsync`;
  - `MembershipAttachmentTests.AUTHZ_GRANT_003_AC6_EachGrantAnAcknowledgementWritesIsRecordedCreatedAsync`;
  - `BootstrapTests.AUTHZ_GRANT_003_AC6_EachGrantBootstrapMakesIsRecordedCreatedNamingItsHolderAsync`;
  - `MaterialisationTests.AUTHZ_GRANT_003_AC6_TheDriftChecksRecordsNameTheGrantsHolderAsTheirSubjectAsync`;
  - the revocation written for a registration's removal: AUTHZ-INHERIT-002 AC5 above.
  - These tests were written before the code, but not run before it.
- Known issue 11, CONV-VCS-004: the truth-table row "a grant written and then revoked, by
  a caller managing grants" (Allowed) restates the unchanged decision in `dd625f63`.
- Q4, CONV-DESIGN-008 AC1 (`a4b096aa`):
  - `LibraryStructureTests.CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList` (the
    packages another package brings are named apart);
  - `LibraryStructureTests.CONV_DESIGN_008_AC1_TheTestingPackageTakesCborFromTheSharedFramework`.
- Q5, LIB-TEST-003 AC1, AC3 and AC5 (`682c59cc`, `9c72eebc`): `TestDeployment`,
  `TestPeople` and `TestSession`.
  - `PeopleTests.LIB_TEST_003_EachPersonIsSignedInAsTheirOwnSubjectAndReachesTheLibrarysRoutesItGrantsAsync`
    (AC1 is proven in the sample host, under D-209 below);
  - `PeopleTests.LIB_TEST_003_AC3_AStaffMemberSignedInWithASoftwarePasskeyMeetsAPhishingResistantGateAtAal2Async`;
  - `PeopleTests.LIB_TEST_003_AC5_AStateChangingRequestThroughATestSessionPassesTheCsrfStagesAsync`;
  - `PeopleTests.LIB_TEST_003_ASessionStepsUpWithThePersonsPasskeyOrPasswordAsync`;
  - `TestDeploymentTests.LIB_TEST_003_AnAddressNoReturnAddressIsRegisteredUnderIsRefusedAsync`.
  - `TestDeployment` against the installed `Janus.Cli`: the sample host's gate runs it
    (LIB-TEST-004).
- Q6, OPS-MIG-001 AC5, `migrate --report` (`10bb562a`):
  - `MigrateTests.OPS_MIG_001_AC5_TheReportOfAnEmptyDatabaseAppliesNothingAndWritesEachMatchAsync`,
    `..._TheReportJudgesThePendingMigrationsAndAppliesNoneAsync` and
    `..._NothingPendingWritesNothingAsync`;
  - `DestructiveOperationReportTests.OPS_MIG_001_AC5_WhatOpsDep001NamesIsDestructive`,
    `..._AConstraintOnACreatedTableIsReportedAndNotDestructive`,
    `..._NothingIsWrittenWhereNothingMatches` and
    `..._EachLineIsTheMigrationTheStatementAndWhetherItIsDestructive`;
  - `ScriptStatementsTests.OPS_MIG_001_AC5_ABlockIsCutIntoItsStatementsWithoutTheScriptsIndentation`,
    `..._ASemicolonThatEndsNoStatementCutsNothing` and `..._ABlockOfBlanksHoldsNoStatement`;
  - `MigrateDocumentTests.OPS_MIG_001_AC5_AnArgumentBesideTheReportIsRefusedByNameAsync`
    and `..._TheReportIsRefusedAsTheRunIsAsync`.
  - The report of an empty database (162 statements, 21 destructive) agrees with the
    pipeline's `destructive-operations.sh` over the same script, compared by hand.
- Q7, AUTHZ-INHERIT-002 AC1 and AC6, `UseUnitOfWorkAsync` (`8d832b9e`, `4e020522`):
  - `UnitOfWorkJoinTests.AUTHZ_INHERIT_002_AC1_AHostsRowAndTheAncestryCommitTogetherAsync`,
    `..._AHostsRowWrittenThroughAJoinedContextIsRolledBackWithTheAncestryAsync` and
    `..._AScopeEndedWithoutACommitLeavesNeitherTheHostsRowNorTheAncestryAsync`;
  - `UnitOfWorkJoinTests.AUTHZ_INHERIT_002_AC6_AUnitOfWorkNotBegunIsAFaultAsync`,
    `..._AContextWhoseOwnConnectionIsOpenIsAFaultAsync`,
    `..._AContextThatHoldsATransactionIsAFaultAsync`,
    `..._AnotherUnitOfWorkWhileOneIsJoinedIsAFaultAsync`,
    `..._CalledAgainForTheJoinedUnitOfWorkItChangesNothingAsync`,
    `..._AfterTheOutermostCommitTheContextUsesTheConnectionStringItWasConfiguredWithAsync`
    and `..._AfterTheOutermostRollbackTheContextUsesTheConnectionItWasConfiguredWithAsync`;
  - `UnitOfWorkTests.AUTHZ_INHERIT_002_AC6_WhatIsJoinedIsTheOpenConnectionAndTransactionWhileBegunAsync`,
    `..._TheNoticeComesOnceAtTheOutermostCommitAsync` and
    `..._TheNoticeComesOnceAtEveryEndWithoutACommitAsync`;
  - `PublicSurfaceTests.AUTHZ_INHERIT_002_TheHostsContextJoinsThroughOneExtensionOnItsDatabaseFacade`;
  - `LibraryStructureTests.OPS_DATA_002_AC2_OnlyTheAccessorTheListenerAndTheHostsJoinTouchAConnection`.
- Q7, the kept-write fault, CONV-DESIGN-003 AC8 and AUTHZ-INHERIT-002 (`366f0e06`,
  `11a339bf`), each operation of CONV-DESIGN-003's list:
  - `AuthenticationServiceTests.CONV_DESIGN_003_AC8_AFactorPresentedInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._AFactorForAHandleThatOpensNothingInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._ADeviceCodeInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._AStepUpInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._APressedLinkInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._ADelegatedSignInInsideAnOpenUnitOfWorkIsAFaultAsync` and
    `..._AnAskInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `BreakGlassServiceTests.CONV_DESIGN_003_AC8_APresentationInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `OidcServiceTests.CONV_DESIGN_003_AC8_AReuseInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `BotDefenceTests.CONV_DESIGN_003_AC8_ACheckInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `DeliveryReportsTests.CONV_DESIGN_003_AC8_AReportInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `RegistrationServiceTests.CONV_DESIGN_003_AC8_ABeginInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._AnAgeInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._ACodeInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._APressedTokenInsideAnOpenUnitOfWorkIsAFaultAsync` and
    `..._TheTermsInsideAnOpenUnitOfWorkAreAFaultAsync`;
  - `IdentifierServiceTests.CONV_DESIGN_003_AC8_ACodeInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._ACodeNamingNothingInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `..._AnEnrolmentSessionsCodeInsideAnOpenUnitOfWorkIsAFaultAsync` and
    `..._APressedTokenInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `CredentialServiceTests.CONV_DESIGN_003_AC8_AConfirmCodeInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `HostCallbackTests.CONV_DESIGN_003_AC8_ACallbackInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - `ProviderEventTests.CONV_DESIGN_003_AC8_AProviderEventInsideAnOpenUnitOfWorkIsAFaultAsync`;
  - the host's case: `UnitOfWorkJoinTests.AUTHZ_INHERIT_002_AC1_AnOperationWhoseRefusalKeepsAWriteFaultsInsideAJoinedUnitOfWorkAsync`.
- Q8, PRIV-RIGHT-003 AC7, the flat machine format with distinct section names (`30b19c3e`):
  - `ExportServiceTests.PRIV_RIGHT_003_AC7_TwoSectionsOfOneAnswerUnderOneNameFailTheExportAsync`;
  - `ExportEndpointTests.PRIV_RIGHT_003_AC7_AHostValueIsKeyedAsALibraryValueIsAsync`.
- REG-PREF-001 AC1 (`9e471029`):
  - `PreferenceDeclarationsTests.REG_PREF_001_AC1_APreferenceNamedAsTheExportCannotKeyItFailsStartup`;
  - `PreferenceDeclarationsTests.REG_PREF_001_AC1_ANameOnlyLikeTheExportsOwnIsDeclared`.
- Q9, the names a contributor answers, judged through the internal `IExportNameRule`
  (`8a6538e5`):
  - `ExportServiceTests.PRIV_RIGHT_003_AC7_EveryNameAContributorAnswersIsJudgedBeforeItIsTakenAsync`
    and `..._ANameOutsideTheRuleFailsTheExportAsync`;
  - `ExportNamesTests.PRIV_RIGHT_003_AC7_ANameInsideTheRuleIsTaken` and
    `..._ANameOutsideTheRuleIsRefused`;
  - `ExportEndpointTests.PRIV_RIGHT_003_AC7_ANameOutsideTheRuleFailsTheExportAsync`.
- Q10, INT-SMS-003 AC3, the contributor's name (`7a21d60e`):
  `StartupValidationTests.INT_SMS_003_AC3_AnExportContributorNamedOutsideTheRuleOrTwiceIsRefusedAsync`.
- Q11 (`96a04ead`, `30423300`):
  - PRIV-RIGHT-005a AC22: `AuthorizationModelTests.PRIV_RIGHT_005a_AC22_TwoEncryptedFieldsOfOneTypeUnderOneNameFailStartup`;
  - AUTHZ-MODEL-004 AC1: `AuthorizationModelTests.AUTHZ_MODEL_004_AC1_AResourceTypeDeclaredTwiceUnderOneNameFailsStartup`;
  - truth-table rows "a grant on a record whose type no other type's declaration shares a
    name with" and "a grant on a record whose type declares no two encrypted fields under
    one name" (both Allowed, restating the unchanged decision).
- Q12, BFF-CAP-002 AC3 and AC4 and BFF-ORDER-001 stage 10, `CapabilityFields`
  (`839fce7d`, `4813435c`):
  - `AuthorizationModelTests.BFF_CAP_002_AC4_TwoCapabilitiesOfOneActionFailStartup`, with
    the truth-table row "a type declaring as capabilities two permissions of one action"
    (Raised);
  - `CapabilityFieldsTests.BFF_CAP_002_AC3_TheFieldsCarryCanAndRequiresAsApiCap001WritesThem`
    and `CapabilityFieldsTests.BFF_CAP_002_TwoPermissionsOfOneActionAreAFaultOfTheCallingCode`;
  - `GateBehaviourTests.BFF_CAP_002_AC3_CapabilitiesReadForAHostsRecordCarryCanAndRequiresAsync`;
  - `CapabilityFieldsTests.BFF_ORDER_001_Stage10_TheStageAddsNothingToAHostAnswerAsync`.
- Q13, the session's rotation (`16c8928f`):
  - BFF-HOST-001 AC5: `RequestAccessTests.BFF_HOST_001_AC5_AStepUpKeepsTheSessionAndLeavesTheCookieBeforeItNoAccessAsync`;
  - BFF-SESS-004 AC1 and AC2: `SessionRotationTests.BFF_SESS_004_AC1_AStepUpRotatesTheIdentifierTheCookieCarriesAndKeepsTheRecordAsync`
    and `SessionRotationTests.BFF_SESS_004_AC2_TheIdentifierBeforeAStepUpIsInvalidatedAsync`;
  - AUTH-SESS-006 AC1 and AC2: `SessionRotationTests.AUTH_SESS_006_AC1_TheIdentifierAfterSignInDiffersFromTheOneBeforeAsync`
    and `..._AC2_SigningInInvalidatesTheFirstContactAsync`;
  - BFF-CSRF-005a AC3: `BreakGlassEndpointTests.BFF_CSRF_005a_AC3_PresentingTheCredentialEndsTheFirstContactAsync`.
- Q14, CONV-DESIGN-007 AC9 (`9b926a49`):
  - `ApplicationJsonTests.CONV_DESIGN_007_AC9_AHostTypeSerializesAsItDoesWithoutTheLibrary`,
    `..._AHostEndpointReadsAndAnswersAsTheHostSetsItAsync`,
    `..._EveryRequestAndResponseTypeIsDeclaredInALibraryContext`,
    `..._EveryBodyIsReadThroughALibraryContext`,
    `..._AMemberSpelledInAnotherCaseIsNotReadAsync` and
    `..._ANumberGivenAsAStringIsRefusedNamingItAsync`;
  - `endpoints.txt` unchanged by it (`LIB_API_001_AC2_TheEndpointsAreTheContract`).
- Q15, IDN-PRIN-001 AC6 and AC7, INT-SMS-003's job part, CONV-VCS-004 (`5274d16b`,
  `b3a7028b`), save what Q20 to Q23 hold:
  - `AuthorizationModelTests.IDN_PRIN_001_AC7_AJobDeclaredWithAPermissionItMayNotExerciseFailsStartup`,
    `..._AJobDeclaredUnderAReservedNameFailsStartup`,
    `AuthorizationModelTests.IDN_PRIN_001_AJobDeclaredTwiceFailsStartup` and
    `AuthorizationModelTests.IDN_PRIN_001_JobsDeclaredWithinTheirRuleBuild`;
  - `StartupValidationTests.INT_SMS_003_AC3_AJobNamedOutsideTheRuleIsRefusedAsync`;
  - `RequestServedTests.IDN_PRIN_001_AC6_ARequestIsServedThroughEitherProfileOrNeitherAndInAScopeWithinItAsync`,
    `..._BackgroundWorkServesNoRequestAsync` and
    `..._AJobsListIsRefusedInARequestAndNotInBackgroundWorkAsync`;
  - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobCaseDecidesTheSameWayThroughBothPathsAsync`,
    with the job rows; `RequestGates.cs` is on the watched list of
    `truth-table-change.sh`.
- Q17, IDN-PRIN-001 AC3 (`1e06c928`):
  `SystemPrincipalTests.IDN_PRIN_001_AC3_TheOperationsAreThoseTheReferenceLists`.
- Q18, LIB-API-001 AC2, LIB-TEST-002 AC1 and REF-001 AC2 (`7083d7bb`):
  - `CommandContractTests.LIB_API_001_AC2_TheCommandsAreTheContract`,
    `..._EveryCommandTheExecutableCarriesDeclaresItsContractAsync`,
    `..._BootstrapRefusesToReadWithoutWhatItDeclaresRequiredAsync` and
    `..._RegisterClientRefusesToReadWithoutWhatItDeclaresRequiredAsync`.
  - LIB-TEST-002 AC1 and REF-001 AC2 run in `release.sh`, which no test runs. They were
    verified by hand in a scratch repository: a dropped refusal code, an added argument,
    swapped output lines, a renamed member and an optional argument made required, each
    against the version bump it needs.
- Q19, LIB-API-002 AC2, the configuration half (`a5efe2f7`):
  - `LibraryStructureTests.LIB_API_002_AC2_TheConformanceSampleChangesPolicyDefaultAtTheConfigurationEndpoint`;
  - `SampleConfigurationTests.LIB_API_002_AC2_TheSamplesPolicyDefaultIsTheOneItsAdministratorSetAsync`.

**Built under D-207**

- Q20 to Q23, a host job's context (`01b0b0b0`, `72ceaa19`):
  - IDN-PRIN-001 AC6, AUTHZ-GATE-005, AUTHZ-GATE-006 (answers 20, 21):
    - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobCaseDecidesTheSameWayThroughBothPathsAsync`
      (six `Jobs` rows, through the check, the expression and the fragment);
    - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobOrganizationCaseDecidesTheWayTheTableSaysThroughTheCheckAsync`
      (six `JobOrganizations` rows, the in-request case among them);
    - `ExplanationTests.IDN_PRIN_001_AC6_ADeclaredJobsRefusalsAreRecordedNamingItsPrincipalAndReasonAsync`.
  - BFF-CAP-002 AC5, AUTHZ-GATE-004, AUTHZ-DERIVE-007 (answers 21, 22), in `JobContextTests`:
    - `BFF_CAP_002_AC5_CapabilitiesGivenAJobsContextAreRefusedInARequestAndOutsideOneAsync`;
    - `IDN_PRIN_001_AC6_TheExplanationGivenAJobsContextIsRefusedAsync`;
    - `AUTHZ_DERIVE_007_AJobIsRefusedTheViewAndIsNotListedByItAsync`.
  - AUTHZ-DERIVE-005 and AUTHZ-GRANT-003 (answer 23; the reading D-208 confirmed), in
    `MaterialisationTests`:
    - `AUTHZ_DERIVE_005_AC7_APersonsRefreshNamesThePersonAndRecordsEachGrantAsync`;
    - `AUTHZ_DERIVE_005_AC7_AJobsRefreshOutsideARequestNamesTheNilGranterAndTheJobAsync`;
    - `AUTHZ_GRANT_003_AC5_TheDriftCheckRecordsEachGrantItWritesOrTakesBackAsync` and
      `AUTHZ_GRANT_003_AC6_TheDriftChecksRecordsNameTheGrantsHolderAsTheirSubjectAsync`.
  - Truth table: the `Jobs` and `JobOrganizations` rows above, and the `Operations` row
    "a fact in the host's data a declared job's refresh materialised, outside a request".
- Q24, Q25 and the observation on a host's own `IUnitOfWork`, AUTHZ-INHERIT-002 AC6 and
  OPS-DATA-002 (`5831c722`, `52797948`, `9f17e984`), in `UnitOfWorkJoinTests`:
  - `AUTHZ_INHERIT_002_AC6_AContextConfiguredWithAConnectionObjectItOwnsIsAFaultAsync`;
  - `..._AContextTakenFromAContextPoolIsAFaultAsync` and `..._AContextFromAPooledFactoryIsAFaultAsync`;
  - `..._AContextConfiguredWithAConnectionStringKeepsItsProvidersTypeMappingsAsync` (a
    database enum read through the provider's mapping after the commit);
  - `..._AContextConfiguredWithANamedConnectionStringKeepsTheStringItResolvedAsync`;
  - `..._AUnitOfWorkTheLibraryDidNotRegisterIsAFaultNamingThatAsync`.
- Q26, LIB-TEST-003 (`f72d64c7`), in `PeopleTests`:
  - `LIB_TEST_003_ACustomerAnswersTheAgeStepWithTheDateOfBirth19900101Async`;
  - `LIB_TEST_003_EachVerificationLinkTheTransportsHoldIsPressedAsync`;
  - `LIB_TEST_003_EachPasskeyIsLabelledSoftwarePasskeyAsync`.
  - The comment in `TestSession.cs` spells the member `webauthn` (`db592be9`), checked by
    reading against `endpoints.txt`.
- The testing package's tests' logging (`b3702a76`, section 3 record 15), CONV-LOG-003 and
  BFF-ERR-002 AC2: verified by a fault thrown in a scratch run, not committed; the failed
  test's output named the exception's type and frames, and not its message.
- Q27, OPS-SEC-001 AC5, LIB-API-001 AC2, LIB-TEST-002 AC1 (`40a2066f`), in `CommandFaultTests`:
  - `OPS_SEC_001_AC5_AFaultThatCarriesNoCodeEndsEveryCommandWithSystemFaultAsync` (each of
    the seven commands);
  - `OPS_SEC_001_AFaultLeavingTheHolderOfADocumentsKeysClearsEveryOneAsync`, which runs no
    command and proves `HeldKeys` alone. That each key-reading
    command clears its keys on a fault was verified by review: each declares
    `using var held = new HeldKeys()`, whose disposal the test proves.
  - `commands.txt` lists `system.fault` for every command, held by
    `CommandContractTests.LIB_API_001_AC2_TheCommandsAreTheContract`.
- Q28, AUTH-OIDC-003 AC4 and CONV-DESIGN-003 AC13 (`67a18bc5`):
  - `OidcServiceTests.AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndRecordsItInOneCommitAsync`
    and `..._AFailedCommitLeavesTheGrantStandingAsync`;
  - `OidcFlowTests.AUTH_OIDC_003_AC4_AReuseRevokesTheSpineTheGrantAndEveryTokenOfItAsync`;
  - `TokenReuseStoreTests.AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndItsRecordInOneCommitAsync`
    and `..._WhereTheCommitFailsNoneOfTheFamilyIsRevokedAsync`.
- Q29, INT-GEN-003 AC3, INT-SMS-005 AC3, BFF-MACH-003 AC2 (`d3222728`):
  `DeliveryReportEndpointTests.INT_GEN_003_AC3_ARejectedReportIsRecordedAgainstItsSourceAndLoggedWithoutItAsync`.
- Q31, AUTH-RECOV-007 AC5 (`a3535a4c`), in `CredentialServiceTests`:
  `AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync` and
  `AUTH_RECOV_007_AC5_ASignInsWriteWhileTheRemovalRunsIsNotOverwrittenAsync` (with fakes;
  the database's part is the `FOR UPDATE` lock the decision reads under).
- Q32, REG-SESS-002 AC4, REG-INV-002 (`8cac8331`), in `RegistrationServiceTests`:
  `REG_SESS_002_AC4_ASignedInBeginCommitsTheAttachmentWithItsRefusalAsync` and
  `CONV_DESIGN_003_AC8_ASignedInBeginCarryingAnInvitationInsideAnOpenUnitOfWorkIsAFaultAsync`.
- Q30, CONV-DESIGN-002, CONV-DESIGN-003 AC13, AUTH-FACT-002b (`3ccfb35c`), seven of the
  eight operations (the eighth under D-208, Q36), in `StepUpBeforeTheUnitOfWorkTests`:
  - `CONV_DESIGN_003_AC13_APolicyReplacementAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `..._ADomainAddedAsks...`, `..._ADomainRemovedAsks...`, `..._AMemberAddedAsks...`,
    `..._AMemberRemovedAsks...`, `..._ARoleDefinedAsks...` and `..._ARoleRemovedAsks...`
    (the signal asked with no unit of work open, its `risk` record standing after the
    step-up refuses);
  - `AUTH_SESS_005b_AC4_AReplacementBelowTheFloorIsRefusedBeforeTheStepUpAsync`;
  - `CONV_DESIGN_002_APolicyReplacementBelowTheSystemPolicyIsRefusedAgainUnderTheLockAsync`,
    `..._ADomainRemovalThatLoosensIsRefusedBeforeTheStepUpAsync`,
    `..._AMemberClosingACycleIsRefusedBeforeTheStepUpAsync` and
    `..._ARoleInUseIsRefusedBeforeTheStepUpAndAgainUnderTheLockAsync`.
  - Truth table: three `StepUpRequired` rows, a member added to a group, a member taken out
    and a role defined, each on a session that has not proved its gate.
- Q34, the part answer 34 leaves in its own organization (`3175faa3`):
  `InvitationServiceTests.REG_INV_001_SystemAdministrationDoesNotStandInForAnInvitationsRolesAsync`
  (the `09` row of `POST /admin/organizations/{id}/invitations`; no criterion states it).

**Built under D-208**

- Q35, D-207 answer 34 with one grant admission, AUTHZ-SCOPE-001 AC4, AUTHZ-SEAM-001,
  AUTHZ-TEST-001 (`4ca48d14`):
  - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryGrantAdmissionCaseDecidesTheWayTheTableSaysThroughTheAdmissionAsync`
    (nine `GrantAdmissions` rows, each with its refusal count and, for a denial, the
    record's permission and organization);
  - `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync`,
    two new `Operations` rows: a grant written and revoked in another organization by a
    system administrator, and one by a system administrator restricted since the gate
    step;
  - `GrantEndpointTests.AUTHZ_SCOPE_001_TheEndpointsCarryTheAdmissionToACreatedOrganizationAsync`
    (AC4 on the real admission is proven under D-209 below).
  - AUTHZ-SEAM-001: by construction, the admission decides each permission through the
    gate's own check; the seam tests pass.
- Q36 and the outermost begins Q30 parked, REG-IDENT-009 AC6, CONV-DESIGN-002,
  CONV-DESIGN-003, AUTH-FACT-002b (`cff1dbde`, `f6c8aae7`):
  - `AccountServiceTests.REG_IDENT_009_AC6_AnEditNamingTheUsernameHeldLeavesAChangeCommittedMeanwhileAsync`,
    `..._CONV_DESIGN_002_AUsernameChangeIsJudgedAgainUnderTheLockAsync` and
    `..._CONV_DESIGN_002_AUsernameTakenIsRefusedBeforeTheUnitOfWorkBeginsAsync`;
  - `StepUpBeforeTheUnitOfWorkTests.CONV_DESIGN_003_AC13_AUsernameChangeAsksTheSignalWithNoUnitOfWorkBegunAsync`
    (the `risk` record standing after the step-up refuses);
  - `CredentialServiceTests.CONV_DESIGN_003_AC8_ATextCodeBegunInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `RecoveryServiceTests.CONV_DESIGN_003_AC8_ARecoveryAskedInsideAnOpenUnitOfWorkIsAFaultAsync`,
    `AuthenticationServiceTests.CONV_DESIGN_003_AC8_ALinkAskedInsideAnOpenUnitOfWorkIsAFaultAsync`
    and `NonExistenceNoticeTests.CONV_DESIGN_003_AnAnswerThatMayFollowTheSignalInsideAnOpenUnitOfWorkIsAFaultAsync`.
  - That no library caller reaches these three operations inside another's unit of work
    was verified by review: every call site into `PhoneSignals.AllowsAsync`, direct and
    through `StepUpGuard` and `StepUpGate.RequireAsync`, was traced. The one path that
    reached `AllowsAsync` inside a unit of work is Q41's.
- Q37, LIB-TEST-003 AC4 (`20374808`):
  - `TransportTests.LIB_TEST_003_TheSmsTransportKeepsEveryMessageByDestinationAsync`;
  - `StandInBalanceTests.LIB_TEST_003_AC4_TheBalanceJobRecordsTheTransportsBalanceAndRaisesNoFailureAsync`
    (PostgreSQL, the library's `sms-balance` job run twice, both readings recorded, no
    `background-job-failed`); the first clause stays proven by
    `StandInTests.LIB_TEST_003_AC4_TheStandInsRaiseNoDegradationAsync`.
- Q38, a job's consent step, IDN-PRIN-001 AC6, AUTHZ-CONCEAL-004 AC4 and AC5 (`91b326d6`,
  `013e948c`; tests only):
  - `ConsentGateTests.IDN_PRIN_001_AC6_AJobsConsentRefusalOnTheOrganizationWideCheckWritesNoRecordAsync`,
    `..._AUTHZ_CONCEAL_004_AC5_AJobsConsentRefusalOnARecordCarriesNoIdentifierAndWritesNoRecordAsync`
    and `..._IDN_PRIN_001_AC6_AJobIsAdmittedOnAConsentBoundPermissionWhereConsentIsHeldAsync`;
  - `JobContextTests.AUTHZ_CONCEAL_004_AC4_AJobsRefusalInsideWorkItRollsBackStandsAsync`;
  - truth table: `Jobs` rows for a consent-bound permission with and without consent, and
    a `JobOrganizations` row.
- Q39 and the refresh's limits, AUTHZ-DERIVE-005 AC7, IDN-PRIN-001 AC6, CONV-DESIGN-007
  (`a12eb66c`), in `MaterialisationTests`:
  - `AUTHZ_DERIVE_005_AC7_AJobsRefreshWhileARequestIsServedThrowsAndWritesNothingAsync`
    (the browser profile, the machine profile, outside both, a scope opened within the
    request);
  - `..._AC7_AReconcilingPrincipalsRefreshWhileARequestIsServedThrowsAndWritesNothingAsync`;
  - `..._AC7_ARefreshByAPrincipalTheRefreshDoesNotAdmitThrowsAndWritesNothingAsync` (an
    organization-scoped principal naming no declared job, a deployment-scoped one that
    may not run `reconciliation`, each through both overloads);
  - `..._AC7_AReconcilingPrincipalsRefreshNamesTheNilGranterAndThePrincipalAsync` and
    `..._AC7_AJobsRefreshOutsideARequestNamesTheNilGranterAndTheJobAsync`;
  - truth table: five `Operations` rows for the refresh.
- Q40, AUTH-OIDC-003 AC2 and AC4 (`86e80903`):
  - `OidcServiceTests.AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync`,
    `..._AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderNoAccountAsync`,
    `..._AC2_AReuseNamingOnlyASessionIsRecordedUnderTheSessionsAccountAsync` and
    `..._AC2_TheTokensAccountIsRecordedBeforeTheGrantsAndTheGrantsBeforeTheSessionsAsync`;
  - `TokenReuseStoreTests.AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync`
    and `..._AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderTheNilSubjectAsync`
    (PostgreSQL, the record's acting identity, `subject` and `details.session`).
  - That the provider's revocation path is gone was verified by reading `TokenReuse`.
- The criteria D-208 brought into line (`cef80da1`, `6c4837a8`, `dc4aff78`; tests only):
  - API-CONV-002 AC2, BFF-ERR-001 AC2, `10` section 1 `authz.denied`, over HTTP:
    `RefusalRecordTests.API_CONV_002_AC2_ARecordedRefusalCarriesItsRecordBesideTheRequestsIdentifierAsync`
    and `..._BFF_ERR_001_AC2_AnUnrecordedRefusalCarriesTheRequestsIdentifierAndNoRecordAsync`;
  - the explanation routes: `RefusalRecordTests.AUTHZ_CONCEAL_004_AC1_TheExplanationRoutesResolveTheIdentifierARefusalsBodyCarriedAsync`;
  - AUTHZ-CONCEAL-004 AC5 for a person:
    `GateBehaviourTests.AUTHZ_CONCEAL_004_AC5_ARestrictionRefusalCarriesNoIdentifierAndWritesNoRecordAsync`,
    `..._AC5_AStepUpRefusalCarriesNoIdentifierAndWritesNoRecordAsync` and
    `ConsentGateTests.AUTHZ_CONCEAL_004_AC5_APersonsConsentRefusalCarriesNoIdentifierAndWritesNoRecordAsync`;
    the account's state verified by reading `AccessGate`, which answers no refusal for it;
  - an `authz.denied` refusing no permission asked of the gate:
    `ExplanationTests.AUTHZ_CONCEAL_004_AnExplanationsOwnRefusalCarriesNoIdentifierAndWritesNoRecordAsync`
    and `JobContextTests.AUTHZ_CONCEAL_004_AJobsRefusalsAskingNoPermissionOfTheGateWriteNoRecordAsync`;
  - AUTHZ-CONCEAL-004 AC4, the disclosed answer:
    `GateBehaviourTests.AUTHZ_CONCEAL_004_AC4_ADisclosedRefusalInsideATransactionThatRollsBackStandsAsync`;
  - AUTHZ-GATE-006 AC3:
    `GateBehaviourTests.AUTHZ_GATE_006_AC3_ARestrictionCommittedAfterTheGateStepRefusesInsideTheUnitOfWorkAsync`
    now also asserts no refusal record; the endpoint tests under AC3 run on the fake gate,
    which forms no record, and cannot decide it;
  - AUTHZ-PRIN-001 AC1: `TruthTableTests.AUTHZ_PRIN_001_AC1_*`, at base, with the grant
    admission's rows above.
- The in-memory test servers' request scopes (`36b09de3`, `24f066ea`, and the sample's in
  `f52c180a`); no criterion states it, so the tests are named for what they show:
  - `ServerInMemoryTests.SendAsync_CustomersRegisteredOneAfterAnother_LeaveTheDeploymentsConnectionsBoundedAsync`
    (conformance, PostgreSQL);
  - `ServerInMemoryTests.SendAsync_ARequestThatResolvesFromItsScope_DisposesWhatItResolvedAsync`
    (hosting).
- LIB-TEST-004, the sample host, and its job in CONV-GATE-001, merged from `part/sample`
  once its gate passed:
  - `7dd72348` feat(samples): add the kite workshop sample host;
  - `1a88bb56` test(core): check that the sample host references no library project;
  - `a1f04bfe` ci(gates): add the sample host's gate script;
  - `dae661a5` ci(gates): run the sample host in the pipeline;
  - `62e6a0c0` build(samples): give the sample host the library's settings and scopes
    (D-207 answer 33);
  - `49a85c98` feat(samples): run the follow-up calls job under its declared permission;
  - `553f5258`, `79939d0d`, `138d363b` and `f52c180a`: the sample's own faults its gate
    showed;
  - `e9adae95` test(samples): give the sample's organization its first grants.
  - D-207 answer 33:
    - `LibraryStructureTests.CONV_LAYOUT_001_TheSampleHostTakesTheLibrarysSettingsWithoutALockfileOrMinVer`;
    - `..._CONV_SETUP_004_AC3_NoRuleButTheFiveOfTheTableIsSetBelowError`,
      `..._CONV_SETUP_004_AC2_TheEditorconfigIsTheOnlyPlaceARuleIsConfigured` and
      `..._CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties`, widened to the
      sample;
    - the format step verified to fail on a whitespace fault put into a sample file and
      taken out again.
  - AC1: `.github/gates/sample-host.sh` and the `sample-host` job; no test decides it.
  - AC2, in `samples/HostSample.Tests`:
    - `WorkshopEndpointTests.LIB_TEST_004_AC2_ARecordIsWrittenAndReadWithItsPersonalFieldEncryptedAsync`,
      `..._AnEndpointIsRefusedWithoutASessionAndOneMarkedAsAnsweringAnyoneIsReachedAsync`,
      `..._ABoundActionIsRefusedWithoutItsStepUpAsync`,
      `..._ARecordRegisteredAndRolledBackLeavesNeitherAsync`,
      `..._ARegistrationIsRemovedAsync`, `..._AnExportCarriesTheHostsSectionAsync`,
      `..._AWithdrawalIsHandledByTheHandlerAsync`, `..._AnObjectionIsHandledByTheHandlerAsync`
      and `..._ACreatedOrganizationIsGivenItsFirstGrantsAndHoldsTheSamplesRecordsAsync`
      (the first administrator writes the first grant, the sample's role
      `workshop-manager`, through `POST /admin/grants` under `system:administer`);
    - `WorkshopEndpointTests.LIB_TEST_004_AWorkshopsListIsFilteredByTheGateAndCarriesCapabilitiesAsync`;
    - `ErasureTests.LIB_TEST_004_AC2_AnErasedCustomersHostFieldNoLongerDecryptsAsync`;
    - `FollowUpCallsTests.LIB_TEST_004_AC2_AJobIsAdmittedForItsDeclaredPermissionAndRefusedForAnotherAsync`
      (the job `follow-up-calls`, `repair:read` alone).
  - AC3: `LibraryStructureTests.LIB_TEST_004_AC3_TheSampleHostReferencesNoProjectOfTheLibrarysSolution`.

**Built under D-209 and the review**

- Q41, AUTHZ-GATE-005, AUTHZ-MODEL-004 (`d5076505`):
  - `AuthorizationModelTests.AUTHZ_GATE_005_AC6_AStepUpGateBoundToALibraryOwnedPermissionFailsStartup`,
    `..._AC6_APurposeBoundToALibraryOwnedPermissionFailsStartup` (each over three of the
    library's permissions) and
    `..._AUTHZ_MODEL_004_AC1_ABindingToALibraryOwnedPermissionIsNamedByItsDeclarationAndField`;
  - truth table: two `Operations` rows, a step-up gate and a purpose bound to a
    library-owned permission.
- Q41, a step-up judged while a unit of work is open, AUTH-FACT-002b, CONV-DESIGN-003
  (`e2b954bd`, `691af92b`):
  - `StepUpGuardTests.AUTH_FACT_002b_AStepUpJudgedWhileAUnitOfWorkIsOpenOffersAsWhereNoSignalIsDeclaredAsync`;
  - `StepUpBeforeTheUnitOfWorkTests.CONV_DESIGN_003_AC13_TheGuardJudgingAHostsGateInsideAUnitOfWorkAsksNoSignalAsync`
    (at the guard; Q48);
  - `StepUpBeforeTheUnitOfWorkTests.CONV_DESIGN_003_AC13_AnOperationAHostCallsInsideItsUnitOfWorkAsksNoSignalAsync`
    (D-209's second case; the signal then asked at `POST /auth/step-up` with no unit of
    work open, before any text is sent);
  - `SendingGovernanceTests.CONV_DESIGN_003_TheSignalAskedInsideAnOpenUnitOfWorkIsAFaultAsync`
    and `..._ARiskAnswersRecordBeginsTheOutermostUnitOfWorkAsync`.
- CONV-DESIGN-003 AC13 for every other operation that judges a step-up (`691af92b`), in
  `StepUpBeforeTheUnitOfWorkTests`, each `CONV_DESIGN_003_AC13_` followed by:
  - the account: `ADeactivationAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnAccountDeletionAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `APasswordSetAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AGeneratorEnrolmentAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ATextCodeEnrolmentBegunAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ATextCodeEnrolmentConfirmedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `RecoveryCodesGeneratedAskTheSignalWithNoUnitOfWorkBegunAsync`,
    `ACredentialRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AKeyRegistrationBegunAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AKeyRegistrationCompletedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnIdentifierAddedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnIdentifierReplacedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnIdentifierRemovedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ALinkAskedForAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AStartToLinkAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AReturnToLinkAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnUnlinkAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnAppPasswordCreatedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnAppPasswordRevokedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnExportAsksTheSignalWithNoUnitOfWorkBegunAsync`;
  - administration: `EverySessionEndedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnAccountsSessionsEndedAskTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnAccountsStandingChangedAsksTheSignalWithNoUnitOfWorkBegunAsync` (suspension,
    reactivation, a restriction lifted, a deletion cancelled),
    `ATakedownAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ATakedownReversedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ABreakGlassGenerationAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AConfigurationChangeAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ARetentionShortenedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnErasureCompletedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `APrivacyRequestFulfilledAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ARestrictionChangedAsksTheSignalWithNoUnitOfWorkBegunAsync` (set, removed, granted),
    `ARecoveryApprovedAsksTheSignalWithNoUnitOfWorkBegunAsync`;
  - organizations and grants: `AGrantAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AGrantRevokedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnOrganizationDeletedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnOrganizationsDeletionCancelledAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `ADomainVerifiedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnInvitationIssuedAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AnInvitationNamingARoleAsksTheSignalWithNoUnitOfWorkBegunAsync`,
    `AMembershipEndedAsksTheSignalWithNoUnitOfWorkBegunAsync`;
  - `AKeyUpgradeOffersNoTextCodeAndAsksNoSignalAsync`.
  - The operations were found from every caller of `PassedAsync`, `PassedToEnrolAsync`,
    `ChallengeAsync` and `IStepUpGate.RequireAsync`. That the TOTP confirm, the
    invitation's acknowledgement, the registration session's enrolments and
    `RequestGates.CostAsync` and `ReachedAsync` ask no signal was verified by reading.
- Q42, INF-BG-001 AC5 (`0713cdf5`):
  `BackgroundWorkerTests.INF_BG_001_AC5_AJobDueBeyondTheTimersLongestDelayRunsWhenItIsDueAsync`.
- Q43, AUTHZ-GATE-006 AC5, AUTHZ-TEST-001 AC2 (`ee0240f8`):
  - `GateBehaviourTests.AUTHZ_GATE_006_AC5_ARestrictedAccountsListsAreRefusedAModifyingPermissionAndHandedAReadingOneAsync`;
  - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryRestrictedCaseDecidesTheSameWayThroughBothPathsAsync`
    (three `Restrictions` rows).
- Q44, OPS-MIG-001 AC4, OPS-SEC-001 AC4 (`3994dc5a`):
  `MigrateDocumentTests.OPS_MIG_001_AC4_ABlankOrNonStringConnectionIsRefusedAsMalformedAsync`;
  `OPS_SEC_001_AC4_MigrateRefusesADocumentCarryingNoConnectionAsync` keeps the document
  with no `connection` member.
- An organization-wide grant naming no held organization, AUTHZ-SCOPE-001 AC4 (`a7ab3e2b`,
  `85670e39`):
  - on the real admission, PostgreSQL, in `TruthTableTests`:
    `AUTHZ_SCOPE_001_AC4_ASystemAdministratorWritesAndRevokesTheFirstGrantOfACreatedOrganizationAsync`,
    `..._AC4_ARestrictedCallerIsRefusedWithNoRecordWhateverItHoldsAsync` and
    `..._AC4_AnOrganizationWideGrantNamingNoOrganizationIsRefusedAgainstNoneAsync`;
  - `GrantEndpointTests.AUTHZ_SCOPE_001_AnOrganizationWideGrantNamingNoOrganizationIsRefusedAsync`
    (the answer's shape over HTTP);
  - truth table: `Operations` rows for a system administrator (Denied) and a restricted
    one (Restricted).
- A grant on a record removed since the gate step (`a98571aa`): an `Operations` row
  (Denied, with its record and correlation), in
  `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync`.
- A job's refresh of another organization's record, AUTHZ-DERIVE-005 AC7 (`91c41459`):
  `MaterialisationTests.AUTHZ_DERIVE_005_AC7_AJobsRefreshOfAnotherOrganizationsRecordThrowsAndWritesNothingAsync`,
  and an `Operations` row (Denied).
- A job's refusal on a record the library holds no row for, AUTHZ-CONCEAL-002 AC3
  (`968f8a1f`):
  `ConcealmentTests.AUTHZ_CONCEAL_002_AC3_AJobsRefusalOnAnAbsentRecordAndARegisteredOneRunTheSameStatementsAsync`,
  and a `Jobs` row restating the unchanged decision.
- PRIV-RIGHT-004 AC3 (`b80d6ada`):
  `GateBehaviourTests.PRIV_RIGHT_004_AC3_ADeclaredJobIsAdmittedToModifyARestrictedSubjectsRecordAsync`.
- Stage 8 whatever the routing order, BFF-HOST-002 AC5, BFF-ORDER-001 AC2 (`0f449a3e`):
  - `HostEndpointTests.BFF_HOST_002_AC5_WithRoutingAfterTheProfileAnUnmarkedHostEndpointStillRefusesABrowserHoldingNoSessionAsync`
    and `..._AC5_WithRoutingAfterTheProfileAnEndedSessionIsAnsweredWithWhatMustBeRedoneAsync`;
  - `HostEndpointTests.BFF_ORDER_001_AC2_MiddlewareAfterLateRoutingRunsForTheRefusalAndNoHostEndpointRunsAsync`;
  - `PublicSurfaceTests.BFF_HOST_002_AC5_TheEntryPointRegistersTheMatcherPolicyOfStageEight`.
- BFF-MACH-003 AC2 (`28d29af5`):
  `HostCallbackTests.BFF_MACH_003_AC2_AForgedCallbackWithAGuessedReferenceIsRejectedAndLoggedAsync`
  now asserts the forged callback's source row and its log lines.
- The migration `OweNothingAfterAnExport`, OPS-MIG-001, D-203 (`30ef0c2f`):
  `OutboxStoreTests.OPS_MIG_001_AnExportRequestedDeliveryIsRemovedBeforeItsKindIsRetiredAsync`
  (PostgreSQL).
- OPS-SEC-001 AC5 (`f04398ed`): the key-clearing case is renamed
  `CommandFaultTests.OPS_SEC_001_AFaultLeavingTheHolderOfADocumentsKeysClearsEveryOneAsync`
  (Q27 above).
- LIB-TEST-003 AC1 (`78d895e7`), in `samples/HostSample.Tests`:
  `PeopleTests.LIB_TEST_003_AC1_ACustomerTheFirstAdministratorAndAStaffMemberEachCallTheWorkshopsOwnEndpointsAsync`;
  the test that carried it is renamed
  `Janus.Testing.Tests.PeopleTests.LIB_TEST_003_EachPersonIsSignedInAsTheirOwnSubjectAndReachesTheLibrarysRoutesItGrantsAsync`.
- `Janus.Testing`'s secrets, the working guide's section 4 (`4d158fae`, `ea444454`):
  - `SoftwarePasskeyTests.LIB_TEST_003_ADisposedPasskeyAnswersNoCeremony` (renamed under D-210);
    that disposal clears the key is verified by review;
    `SoftwarePasskey` is disposable (`PublicAPI.Unshipped.txt`), and `TestSession`
    disposes its passkey, verified by reading;
  - the key document's buffer cleared after the copy: verified by reading, since a test
    of the testing package holds no grant to see the buffer.
- The comment in `SoftwarePasskey.cs` spells the member `webauthn` (`2469de9a`), checked
  by reading against `endpoints.txt`.
- The sample's capacity check, CONV-DESIGN-003 (`410b1373`):
  `CapacityTests.CONV_DESIGN_003_AC6_TwoBookingsOfAWorkshopsLastPlaceLeaveOneAsync`
  proves the outcome.
- The sample's packages from the feed alone, LIB-TEST-004 (`1cfa788e`, section 3 record
  22): verified by the sample host's gate run (section 5); no test decides it.
- The product-name scan in a linked checkout (`68aaf9ec`, section 3 record 20).
- Not built: the review finding on `ExportService.cs`, parked by Q47.

**Built under D-210 and the review**

- The servicing release of 2026-10-13, CONV-DEP-004 (`2c107c87`): SDK 10.0.402 in
  `global.json`, `Microsoft.EntityFrameworkCore`, its relational and design packages and
  `dotnet-ef` at 10.0.13, each listing checked on nuget.org first; the lockfiles
  regenerated. Q49 asks about the release of 2026-10-25.
- Q46, BFF-ORDER-001 AC2, BFF-OWN-001 (`677647b0`, `f39c31e1`; no runtime code changed):
  - `HostEndpointTests.BFF_ORDER_001_AC2_WithRoutingAfterTheProfileAnEnrolmentRouteAnswersAsForNoEnrolmentSessionAsync`;
  - `HostEndpointTests.BFF_ORDER_001_AC2_WithRoutingAfterTheProfileAnUnboundRequestIsMalformedAsBeforeAsync`
    (a route value, a body and a host endpoint's query value);
  - the documentation of `UseBrowserProfile`, `SessionResolution` and `MalformedRequest`
    brought into line with BFF-OWN-001, verified by reading.
- Q48, AUTH-STEP-002 AC10 (`0d7f26b7`):
  - `StepUpGatesTests.AUTH_STEP_002_AC10_AReportedGateMetAtItsFirstAskIsMetAtASecondAfterItsMaximumAgeAsync`
    and `..._AC10_AReportedGateUnmetAtItsFirstAskIsUnmetAtASecondAfterTheReportMeetsItAsync`;
  - truth table: a `Requests` table of four rows (the report and the session, each met
    then aged and unmet then met), in
    `TruthTableTests.AUTH_STEP_002_AC10_EveryRequestCaseAnswersTheSecondAskAsTheFirstThroughBothPathsAsync`.
  - The provider's judgement is kept for the request by acting subject and gate.
- Q48, CONV-DESIGN-003 AC13's first case (`e5c434de`):
  `HostGateInsideTheUnitOfWorkTests.CONV_DESIGN_003_AC13_AHostsGateUnmetAtItsAskInsideItsUnitOfWorkAsksNoSignalAsync`
  (PostgreSQL, the gate a host asks); the guard's case is renamed
  `StepUpBeforeTheUnitOfWorkTests.CONV_DESIGN_003_AC13_TheGuardJudgingAHostsGateInsideAUnitOfWorkAsksNoSignalAsync`.
- `PhoneSignalAudit` asks the unit of work (`e2b16a48`), AUTH-FACT-002b, CONV-DESIGN-003:
  proven through the real stack by the test above. A unit of work that is not the
  library's is a fault, as `UnitOfWorkJoin`'s is (D-207).
- The comment in `StepUpBeforeTheUnitOfWorkTests` on whose sessions the called operation
  ends (`f2c903d6`).
- OPS-DEP-004 AC7 (`56996663`): no committed test (Q51). Verified by running the changed
  script as the pipeline runs it, gitleaks 8.30.1 at its pinned SHA-256, against scratch
  repositories built at run time, with an invented credential:

  | Scratch repository | Script before | Script after |
  |---|---|---|
  | a credential alone | fails, one finding | fails, one finding |
  | a credential on a line carrying the inline allow marker | passes | fails, one finding |
  | a `.gitleaksignore` at the root, untracked, listing the credential | passes | fails, naming the file |
  | the same, tracked | passes | fails, naming the file |
  | an empty `.gitleaksignore` tracked in a folder, history clean | passes | fails, naming the file |
  | a `.gitleaksignore` committed and removed, tree clean | passes | passes |
  | a clean repository | passes | passes |

  The changed script over this repository's full history found nothing.
- OPS-SEC-001 AC4 (`08436851`; no runtime code changed):
  `BootstrapRefusalTests.OPS_SEC_001_AC4_EveryCommandRefusesAConnectionAbsentBlankOrNotAStringAsMalformedAsync`
  (`bootstrap`, `rotate-kek`, `rotate-fingerprint-key`, `configure`, `replay-erasures` and
  `register-client`; a connection absent, empty, blank, a number, null and an object).
- CONV-SETUP-001 (`c4ad99a9`): `MSBuildTreatWarningsAsErrors` in `Directory.Build.props`;
  `LibraryStructureTests.CONV_SETUP_001_AC2_AWarningMSBuildRaisesFailsTheBuildAsync`, and
  `CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties` covering the new
  property. No warning of MSBuild or the SDK was raised in the library's solution or the
  sample host's, so none was fixed.
- CONV-SETUP-004 AC3 (`4bbc960e`):
  `LibraryStructureTests.CONV_SETUP_004_AC3_ASettingThatSetsARuleBelowErrorIsFoundInAnyBuildFile`
  (one case for each setting `08` names, through the predicate the tree scan
  `CONV_SETUP_004_AC2_TheEditorconfigIsTheOnlyPlaceARuleIsConfigured` uses).
- AUTHZ-GATE-006, AUTHZ-DERIVE-005, the documentation of `IAccessGate.FilterAsync` and
  `FragmentAsync`, `IDerivationMaterialiser` and `DerivationMaterialiser` (`e96ade6d`):
  verified by reading against `AccessGate` and `DerivationMaterialiser`.
- AUTHZ-SCOPE-001 AC4 (`0de297af`):
  `TruthTableTests.AUTHZ_SCOPE_001_AC4_ARestrictedCallerIsRefusedWithNoRecordWhateverItHoldsAsync`
  asserts the grant the refused revocation names still stands.
- The changelog line on a job's unregistered record (`65e4ccbb`).
- LIB-TEST-003 (`04318206`): the disposed passkey's case is renamed
  `SoftwarePasskeyTests.LIB_TEST_003_ADisposedPasskeyAnswersNoCeremony`; that disposal
  clears the key is verified by review.
- CONV-GATE-001 AC2: the sample host's job added to the checks the protection of `main`
  requires (below).
- Not built: D-210 answer 47's fault entry (Q50), and the review's finding on
  `SessionRequirementPolicy`'s dynamic endpoints (Q52).

**Built under D-211**

- Q49, CONV-DEP-004 (`f05bcbe7`): SDK 10.0.403 in `global.json`;
  `Microsoft.EntityFrameworkCore`, its relational and design packages and `dotnet-ef` at
  10.0.14, each listing checked on nuget.org first (published 2026-10-25); the lockfiles
  regenerated. The gates ran on SDK 10.0.403 of the machine's system install (section 5).
  The two Microsoft packages that follow the runtime and reach the solution only
  transitively are not moved (Q53).
- Q50, PRIV-RIGHT-003 AC5, BFF-ERR-002 AC2 (`9609c95e`):
  - `IFaultEntry`, internal to `Janus.Privacy`, which `Janus.Hosting`'s `FaultEntry`
    implements by writing `FaultLog`'s entry as one event of its own at Error, naming the
    contributor; the export keeps a contributor's fault through it, and the error
    `IExports.AssembleAsync` returns is unchanged;
  - `ExportServiceTests.PRIV_RIGHT_003_AC5_WhatAContributorThrewIsKeptByTheFaultEntryAndNotInTheErrorAsync`,
    and every faulted export of those tests held to an error carrying the `contributor`
    detail alone;
  - `ExportEndpointTests.PRIV_RIGHT_003_AC5_AFaultingContributorsFaultIsLoggedByItsTypeAndFramesUnderItsRequestAsync`:
    the entry's `RequestId` scope is the correlation identifier the 500 answers with, and
    no line of the log holds the exception's message.
  - The test deployment runs its pipeline without a server, so it opens the scope the
    server opens around a request, carrying its `RequestId`;
    `StartupValidationTests.BFF_LOG_002_AC4_WithNoOtherRuleNoRequestEntryIsWrittenAndEachEntryCarriesItsRequestAsync`
    proves the server opens it.
- Q52, BFF-HOST-002 AC6, BFF-ORDER-001 stage 8 (`0e75e020`):
  - stage 8 holds every endpoint routing selects to a session unless it carries the
    mark, whatever its type. The two it holds to none are `HttpMethodMatcherPolicy`'s 405
    endpoint and `AcceptsMatcherPolicy`'s 415, each with no metadata and the display name
    ASP.NET Core 10.0.14 gives it, `405 HTTP Method Not Supported` and
    `415 HTTP Unsupported Media Type`, read in both policies' source at the tag `v10.0.14`
    of `dotnet/aspnetcore` and in the installed `Microsoft.AspNetCore.Routing.dll` 10.0.14;
  - `AllowNoSessionAttribute` (public, sealed) in `Janus.Hosting`, read as
    `AllowNoSession()` is, the nearer mark standing, in `PublicAPI.Unshipped.txt` and in
    `PublicSurfaceTests.LIB_API_002_AC1_InternalTypesAreNotPubliclyAccessible`;
  - `SessionRequirementPolicy.AppliesToEndpoints` answers true where the candidates hold
    a dynamic endpoint (the review's finding 1);
  - `HostEndpointTests.BFF_HOST_002_AC6_AnUnmarkedActionAFallbackResolvesToRefusesABrowserHoldingNoSessionWhereverRoutingIsAddedAsync`,
    `..._AC6_AnActionMarkedWithAllowNoSessionIsReachedWithoutASessionWhereverRoutingIsAddedAsync`
    (the mark on the action and on its controller),
    `..._AC6_AMethodAServedHostPathDoesNotTakeIsNotFoundWhereverRoutingIsAddedAsync` and
    `..._AC6_TheFrameworksOwnRejectionsAreTheEndpointsStageEightHoldsToNoSessionAsync`,
    each with routing before the browser profile and after it. Run against the rule
    before the change, and against the new rule without the framework's two endpoints
    set apart, the first and the third failed as they should.
  - Not built: the tests of a Razor page reached through `MapFallbackToPage` (Q54).
- An organization's capabilities, BFF-CAP-002 AC6 and AC5, AUTHZ-GATE-005 AC1
  (`bd065aee`):
  - `CapabilitiesAsync` given the type `organization` answers, for each organization,
    the permissions its organization-wide grants allow, any declared permission asked,
    every one judged in one statement (`PermissionRule.ToOrganizationPage`); no refusal
    recorded and none counted; `requires` as for a record, never `consent`; a host job's
    context refused `authz.denied`;
  - `GateBehaviourTests.BFF_CAP_002_AC6_AnOrganizationsCapabilitiesAreWhatItsWholeOrganizationGrantsAllowAsync`
    (two organizations, a host permission behind a step-up gate, one bound to a
    consent-based purpose and one of the library's; one statement over the grants; no
    refusal recorded, no spike);
  - `PermissionRuleTests.BFF_CAP_002_AC6_APageOfOrganizationsIsOneStatementOverTheGrantsNamingNoRecord`;
  - `JobContextTests.BFF_CAP_002_AC5_CapabilitiesGivenAJobsContextAreRefusedInARequestAndOutsideOneAsync`
    asks the organization's page beside the record's;
  - truth table: three rows of `Operations` (a role granted on the whole organization, a
    deny there, a grant on a record of it alone), each the page and the
    organization-wide single check agreeing.
- Q51, OPS-DEP-004 AC7: decided by running the step's script against repositories built
  at run time, as criteria 1 to 6 are; verified so under D-210 (above). No committed test.

**Built under D-212 to D-216**

- The roles a role hands out, AUTHZ-GRANT-004, AUTHZ-GRANT-005 (`44a3a518`):
  - a role's `handsOut`, defined, declared and seeded with it, kept by name in the
    migration `AddRoleHandsOut`, and in `authz.role.defined`'s `before` and `after`;
  - `AuthorizationModelTests.AUTHZ_GRANT_005_ADeclaredRoleHandsOutOnlyARoleTheDeploymentWillHold`;
  - `RoleEndpointTests.AUTHZ_GRANT_004_AC6_ARoleAnotherRolesHandsOutNamesIsNotRemovedAsync`,
    `RoleEndpointTests.AUTHZ_GRANT_005_ARoleCarriesTheRolesItsHoldersHandOutAsync` and
    `RoleEndpointTests.AUTHZ_GRANT_005_AHandsOutOutsideTheRuleOrNamingNoRoleIsRefusedAsync`;
  - `RoleStoreTests.AUTHZ_GRANT_005_TheRolesARoleHandsOutAreKeptWithItAsync`;
  - truth table: "a role handing itself out defined, on a session that has not proved
    its gate, by a caller managing roles", `StepUpRequired`.
- `authz.handout.byholding`, AUTHZ-GRANT-005, `10` section 4.5a (`7eccc884`):
  `SettingDirectionTests.AUTHZ_GRANT_005_HandingOutByHoldingShipsOffAndTurningItOnLoosens`.
- A grant, a deny and a revocation held to the hand-out rule, AUTHZ-GRANT-005
  (`f0c28e87`):
  - `authz.role.notassignable` and `authz.grant.self`, each refusal judged at the gate
    step and again inside the unit of work;
  - with `authz.handout.byholding` on, the actor's stored grants and materialised
    derivations count and a live derivation admits nothing (D-216 answer 56);
  - `HandOutTests.AUTHZ_GRANT_005_AC1_ARoleTheActorsRoleListsIsHandedOutAndNoOtherAsync`,
    `..._AC2_ByHoldingARoleIsHandedOutWhereTheGrantsAdmitAllItCarriesAsync`,
    `..._AC3_AGrantWideningTheActorsOwnAccessIsRefusedAsync`,
    `..._AC5_ADenyAndARevocationAreJudgedAsAGrantIsAsync`,
    `..._AC6_ASystemAdministratorHandsOutAnyRoleToItselfIncludedAsync` and
    `..._AC7_ALiveDerivationAdmitsNothingAndAMaterialisedOneDoesAsync`;
  - truth table: "a grant of a role no role of the caller hands out, by a caller
    managing grants" and "a revocation of a grant of a role no role of the caller hands
    out, by a caller managing grants", `NotAssignable`; "an allow grant to the caller
    itself, of a role the caller hands out", `WidensOwnAccess`.
- A change of a group's members, AUTHZ-GRANT-005, AUTHZ-GROUP-001 (`f9449284`):
  - `HandOutTests.AUTHZ_GRANT_005_AC1_AGroupReachingOnlyRolesTheActorHandsOutTakesAMemberAsync`,
    `..._AC3_JoiningAGroupOrLeavingADenyIsRefusedAsync` and
    `..._AC5_ARemovalFromAGroupIsJudgedAsAGrantIsAsync`;
  - truth table: "a member added to a group holding a role no role of the caller hands
    out, by a caller managing groups" and "a member taken out of a group holding a role
    no role of the caller hands out, by a caller managing groups", `NotAssignable`; "the
    caller added to a group, by a caller managing groups" and "the caller taken out of a
    group holding a deny, by a caller managing groups", `WidensOwnAccess`.
- An invitation's roles, AUTHZ-GRANT-005, REG-INV-001, REG-INV-002 (`eb4ea421`):
  `InvitationServiceTests.AUTHZ_GRANT_005_AC1_AnInvitationNamesOnlyARoleTheInviterHandsOutAsync`
  and `InvitationServiceTests.AUTHZ_GRANT_005_AC3_AcknowledgingAnInvitationOneIssuedIsRefusedAsync`
  (`Janus.Authentication.Tests`).
- A role change within its actor's reach, AUTHZ-GRANT-005, AUTHZ-GRANT-004 (`60734c0d`,
  `1a6ed945`):
  - the permissions and `handsOut` a role carries before the change and after it, a
    removal included;
  - `HandOutTests.AUTHZ_GRANT_005_AC4_ARoleChangeBeyondTheActorsReachIsRefusedAsync`;
    `HandOutTests.AUTHZ_GRANT_005_AC6_ASystemAdministratorHandsOutAnyRoleToItselfIncludedAsync`
    covers a system administrator's role change beyond the reach of its other roles;
  - truth table: "a role carrying a permission the caller's grants do not admit
    defined, by a caller managing roles" and "a role handing out a role no role of the
    caller hands out defined, by a caller managing roles", `NotAssignable`.
- What an ended membership reached, IDN-MEM-001, AUTHZ-GRANT-005 (`739c8962`):
  - the end now takes `{ reason }` (D-216 answer 55); in the end's unit of work, the
    grants a person gave the account in the organization are revoked and the account
    leaves its groups, and where the organization is the administrative one, its grants
    in every organization it is no member of are revoked too, each record with the end's
    reason and actor; the end is judged as those revocations and removals are;
  - `InvitationServiceTests.IDN_MEM_001_AC3_AnEndRevokesWhatItReachesWithItsReasonAsync`,
    `..._AC8_AnEndReachingARoleTheActorDoesNotHandOutIsRefusedAsync` and
    `..._AC9_AnEndWithoutAReasonIsRefusedAsync` (`Janus.Authentication.Tests`);
  - `InvitationEndpointTests.IDN_MEM_001_AC9_AnEndWithoutAReasonIsMalformedAsync`;
  - `InvitationServiceTests.IDN_MEM_001_AC4_EndingTheAdministrativeMembershipStopsItsGrantsAsync`,
    `..._AC3_EndingAMembershipRevokesWhatAPersonGaveAndLeavesItsGroupsAsync` and
    `..._AC3_EndingTheAdministrativeMembershipRevokesGrantsWhereTheAccountIsNoMemberAsync`
    (`Janus.Hosting.Tests`, integration);
  - truth table (`fdd6c639`): "an end of a membership reaching a role carrying system
    administration that a role of the caller lists, by a caller managing memberships",
    `NotAssignable`, the membership left current; "a grant of a role carrying system
    administration that a role of the caller lists, by a caller managing grants",
    `Denied`, as `09` section 8 answers a grant of such a role without
    `system:administer` (OPS-CFG-007). `739c8962` had written the grant's row as
    `NotAssignable`, which the pipeline's runs on `f90c4460` refused (section 5).
- A membership's end set in advance, IDN-MEM-001 (`c17ce71e`, `23906caa`):
  - `PUT` and `DELETE /admin/organizations/{id}/memberships/{subject}/end`, the
    membership's `ends_at`, `end_set_by` and `end_reason` in the migration
    `AddMembershipEnds`, and `identity.membership.endset`;
  - `end_set_by` refuses the max UUID (`ck_memberships_end_set_by_not_max_uuid`, the
    migration `RefuseTheMaxUuidWhereAnEndNamesWhoSetIt`, `23906caa`), which
    `SchemaContractTests.PRIV_RIGHT_005a_AC18_EveryColumnThatCanHoldASubjectRefusesTheMaxUuidAsync`
    holds every subject column to;
  - `MembershipTests.IDN_MEM_001_AC7_AnEndSetInAdvanceIsMovedAndCancelled` and
    `MembershipTests.SetEnd_AMembershipThatHasEnded_Throws`;
  - `InvitationServiceTests.IDN_MEM_001_AC7_AnEndIsSetMovedAndCancelledAsync`,
    `..._AC7_AnEndNotAfterThePresentIsInvalidAsync`,
    `..._AC8_AnEndSetReachingARoleTheActorDoesNotHandOutIsRefusedAsync` and
    `..._AnEndSetTakesTheRefusalsOfAnEndNowAsync` (`Janus.Authentication.Tests`);
  - `InvitationEndpointTests.IDN_MEM_001_AC7_AnEndIsSetAndCancelledAsync`;
  - `InvitationServiceTests.IDN_MEM_001_AC7_AnEndSetIsKeptOnTheMembershipAsync`
    (`Janus.Hosting.Tests`, integration);
  - not built: the job's pass that ends the membership at its instant (Q57).
- The principal `membership-ends` reserved, IDN-PRIN-001 (`b5cdfb36`):
  - `AuthorizationModelTests.IDN_PRIN_001_AC7_AJobDeclaredUnderAReservedNameFailsStartup`
    with `membership-ends`;
  - truth table: "a declared job under a name no library principal takes, asking a
    permission its declaration names, outside a request", `Allowed`, a row restating an
    unchanged decision.
- IDN-MEM-001's other criteria stand on the tests that proved them before:
  - AC1: `MembershipTests.IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesNamed` and
    `OrganizationStoreTests.IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesAndTheRowAsync`;
  - AC2: `MembershipTests.IDN_MEM_001_AC2_TheRecordCarriesItsBeginningAndItsEnd` and
    `MembershipEndingTests.IDN_MEM_001_AC2_TheMembershipEndsAndItsRecordStaysAsync`;
  - AC5 and AC6: their tests in `InvitationServiceTests` (`Janus.Authentication.Tests`),
    each now ending with a reason.
- The contract files, LIB-API-001 AC2 (`1baea90e`): `error-statuses.txt` gains
  `authz.grant.self` and `authz.role.notassignable`, `configuration-keys.txt`
  `authz.handout.byholding`, `endpoints.txt` the routes and refusals above, and
  `schema.txt` the membership's end and a role's `hands_out`; the commits that changed
  them had left them as they were, which the pipeline's runs on `f90c4460` refused.
- D-215 answer 53: nothing to build. D-215 answer 54: the page case verified in review
  (below, under criteria no test decides).

**Items the round proves that this section did not list**

- AUTH-SESS-005b:
  - AC1:
    - `SessionServiceTests.AUTH_SESS_005b_AC1_AStaffSessionBelowAal2IsRefusedAsync` (at base);
  - AC2:
    - `SessionServiceTests.AUTH_SESS_005b_AC2_AFurtherPrimaryFactorDoesNotLowerTheFloorAsync` (at base);
  - AC3:
    - `SessionServiceTests.AUTH_SESS_005b_AC3_ABreakGlassSessionIsEstablishedDespiteTheFloorAsync` (at base);
  - AC4:
    - `OrganizationPolicyEndpointTests.AUTH_SESS_005b_AC4_AReplacementBelowTheAdministrativeFloorIsRefusedAndChangesNothingAsync`;
    - `StepUpBeforeTheUnitOfWorkTests.AUTH_SESS_005b_AC4_AReplacementBelowTheFloorIsRefusedBeforeTheStepUpAsync`;
  - Also:
    - `OrganizationPolicyEndpointTests.AUTH_SESS_005b_TheAdministrativeOrganizationKeepsItsFloorAsync` (at base);
- AUTH-STEP-002:
  - AC1:
    - `StepUpTests.AUTH_STEP_002_AC1_AGateIsThreeValuesAndNamesNoFactor` (at base);
  - AC2:
    - `StepUpTests.AUTH_STEP_002_AC2_TheDecisionReadsTheSessionAndWhatIsReachable` (at base);
  - AC3:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC3_AtAStepUpASessionThatMeetsTheStrictestOfThePolicysGatesIsNotChallengedAsync`;
    - `GateBehaviourTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync` (at base);
    - `StepUpGatesTests.AUTH_STEP_002_AC3_ASessionThatMeetsAHostsGateIsNotChallengedAsync` (at base);
    - `StepUpTests.AUTH_STEP_002_AC3_AGateWhoseLevelIsDelegatedAsksNoMaximumAge`;
    - `StepUpTests.AUTH_STEP_002_AC3_AGateWhoseLevelIsDelegatedCountsNothingReachedUpToTheLastDowngrade`;
    - `StepUpTests.AUTH_STEP_002_AC3_ASessionThatAlreadyProvedItIsNotChallenged` (at base);
    - `StepUpTests.AUTH_STEP_002_AC3_AnEnrolmentGateAboveDelegatedAsksItsMaximumAge`;
    - `StepUpTests.AUTH_STEP_002_AC3_EvidenceOlderThanTheMaximumAgeIsNotEnough` (at base);
    - `StepUpTests.AUTH_STEP_002_AC3_ProofAttainedUpToTheLastDowngradeIsNotCounted`;
  - AC4:
    - `AssuranceTests.AUTH_STEP_002_AC4_AnOwnCombinationProvesWhatItReaches` (at base);
    - `StepUpTests.AUTH_STEP_002_AC4_EveryCombinationThatReachesTheGateIsOffered` (at base);
  - AC4a:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4a_AtAStepUpAPasswordWithATextCodeIsOfferedUnlessTheWeakestGateAsksPhishingResistanceAsync`;
    - `StepUpTests.AUTH_STEP_002_AC4a_AnSmsCodePassesOnlyWhereRelayResistanceIsNotAsked` (at base);
  - AC4b:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4b_AtAStepUpOnlyPhishingResistantCombinationsAreOfferedWhereTheWeakestGateAsksItAsync`;
    - `StepUpTests.AUTH_STEP_002_AC4b_OnlyRelayResistantCombinationsPassSuchAGate` (at base);
  - AC4c:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4c_APasswordAtOneCallAndAGeneratedCodeAtTheNextPassAGateDeclaredAal2Async`;
  - AC4d:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4d_APasswordAloneIsAnsweredFactorRequiredReportingAal1AndRenewsAal1AloneAsync`;
  - AC4e:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4e_ATextCodeAskWithNoFactorAcceptedReportsDelegatedAndWritesNothingAsync`;
  - AC4f:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC4f_WhereOneGateAsksPhishingResistanceAPasswordAndAGeneratedCodeAreOfferedAndWriteAal2Async`;
  - AC5:
    - `StepUpTests.AUTH_STEP_002_AC5_APasswordOnlyAccountPassesItsOwnGate` (at base);
  - AC6:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC6_AtAStepUpEveryCombinationThatReachesTheWeakestGateFieldByFieldIsOfferedAndNoOtherAsync`;
    - `StepUpTests.AUTH_STEP_002_AC6_NothingThatFallsShortIsOffered` (at base);
  - AC7:
    - `StepUpTests.AUTH_STEP_002_AC7_APendingLossReportIsShownWithItsCompletion` (at base);
    - `StepUpTests.AUTH_STEP_002_AC7_AnAccountBelowTheGateIsToldToEnrol` (at base);
    - `StepUpTests.AUTH_STEP_002_AC7_AnAccountThatCannotPresentItIsToldToReportALoss` (at base);
  - AC8:
    - `StepUpTests.AUTH_STEP_002_AC8_RegisteringANewFactorTypeChangesNoGate` (at base);
  - AC9:
    - `AuthenticationServiceTests.AUTH_STEP_002_AC9_AFactorThePolicyDoesNotPermitIsNotOfferedAndIsRefusedWhenPresentedAsync`;
  - Also:
    - `AuthenticationServiceTests.AUTH_STEP_002_ARefusalOfAStepUpIsJudgedAgainstAndCarriesTheWeakestOfThePolicysGatesAsync`;
    - `AuthenticationServiceTests.AUTH_STEP_002_ASecondFactorAloneAtAStepUpWritesNoInstantAndReportsDelegatedAsync`;
    - `AuthenticationServiceTests.AUTH_STEP_002_AStepUpAnswersFactorRequiredUntilTheStrictestOfThePolicysGatesIsReachedAsync`;
    - `AuthenticationServiceTests.AUTH_STEP_002_AStepUpNoCombinationCanCompleteIsAnsweredCompleteWithWhatWasReachedAndEndsAsync`;
    - `AuthenticationServiceTests.AUTH_STEP_002_ATextCodeAskAfterAPasswordReportsWhatTheFactorsAcceptedReachAsync`;
    - `AuthenticationServiceTests.AUTH_STEP_002_ATextCodeAskedForAtAStepUpWritesNothingIntoTheSessionAsync`;
    - `CredentialFlowTests.AUTH_STEP_002_AnAccountReachingAal2IsSentToStepUpAsync` (at base);
    - `StepUpGatesTests.AUTH_STEP_002_AnotherPersonsSessionMeetsNoGateAsync` (at base);
    - `StepUpGuardTests.AUTH_STEP_002_AGateNamedInTheCatalogueCostsItsOwnValuesAsync` (at base);
    - `StepUpGuardTests.AUTH_STEP_002_AnotherPersonsSessionIsRefusedAsync` (at base);
- CONV-CODE-008:
  - AC1:
    - `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnAsynchronousMethodWithoutACancellationTokenAsync` (at base);
    - `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnOwnInterfacesMemberWithoutACancellationTokenAsync`;
    - `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_ReportedOnBlockingOnATaskAsync` (at base);
    - `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_SilentOnAFrameworkInterfacesMemberAndOnTheRollbackAsync`;
    - `BlockingAndCancellationAnalyzerTests.CONV_CODE_008_AC1_SilentOnAnAwaitWithACancellationTokenAsync` (at base);
    - `DiscardedResultAnalyzerTests.CONV_CODE_008_AC1_ReportedOnADiscardedOutcomeAsync` (at base);
    - `DiscardedResultAnalyzerTests.CONV_CODE_008_AC1_SilentOnAnOutcomeThatIsHandledAsync` (at base);
    - `NeverLoggedValueAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAMarkedValuePassedToALoggingCallAsync` (at base);
    - `NeverLoggedValueAnalyzerTests.CONV_CODE_008_AC1_SilentOnAnUnmarkedValuePassedToALoggingCallAsync` (at base);
    - `PermittedOutcomeFromCatchAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAPermissionReturnedFromACatchAsync` (at base);
    - `PermittedOutcomeFromCatchAnalyzerTests.CONV_CODE_008_AC1_ReportedOnASuccessfulOutcomeReturnedFromACatchAsync` (at base);
    - `PermittedOutcomeFromCatchAnalyzerTests.CONV_CODE_008_AC1_SilentOnARefusalReturnedFromACatchAsync` (at base);
    - `SwallowedExceptionAnalyzerTests.CONV_CODE_008_AC1_ReportedOnACatchThatOnlyCarriesOnAsync` (at base);
    - `SwallowedExceptionAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnEmptyCatchAsync` (at base);
    - `SwallowedExceptionAnalyzerTests.CONV_CODE_008_AC1_SilentOnACatchThatDealsWithTheExceptionAsync` (at base);
    - `UnsealedTypeAnalyzerTests.CONV_CODE_008_AC1_ReportedOnAnUnsealedClassAsync` (at base);
    - `UnsealedTypeAnalyzerTests.CONV_CODE_008_AC1_SilentOnASealedClassAndAnAbstractBaseAsync` (at base);
  - AC2:
    - `LibraryStructureTests.CONV_CODE_008_AC2_TheAnalyserProjectIsReferencedAsAnAnalyser` (at base);
- CONV-DESIGN-006:
  - AC1:
    - `PublicSurfaceTests.CONV_DESIGN_006_AC1_NoTypeDerivesFromControllerBase` (at base);
  - AC3:
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC3_AProviderRoundTripDeclaresTheCodesItsRedirectCarries`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC3_AnEndpointDeclaresWhatItsSectionGivesIt`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC3_EveryEndpointCarriesADeclaration`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC3_TheSignOnDeclaresTheCodeItsRedirectCarries`;
  - AC4:
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_ADeclaredCodeAndThePipelinesPass`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_AMountingAnswerPassesOnlyWhereTheMountingGivesIt`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_ARedirectCarriesNoCodeByItsMounting`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_ARedirectCarryingADeclaredCodePasses`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_ARedirectCarryingAnUndeclaredCodeFailsTheTest`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_ARefusalTheHostCannotReadFailsTheTest`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_AnUndeclaredCodeFailsTheTest`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_TheHostHoldsEveryRedirectItCarriesAsync`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_TheHostHoldsEveryResponseItCarriesAsync`;
    - `EndpointAnswersTests.CONV_DESIGN_006_AC4_TheMachineCallbacksRedirectIsOutsideTheCheck`;
  - AC5:
    - `AppPasswordFlowTests.CONV_DESIGN_006_AC5_AnIdentifierOutsideItsFormIsMalformedAsync`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC5_ANavigationRouteBindsNoTypedValue`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC5_AValueThatDoesNotParseIsRefusedNamingItAsync`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AC5_EachHandlersTypedValuesAreTheOnesItsEndpointDeclares`;
    - `ProviderSignInTests.CONV_DESIGN_006_AC5_ARefusedStartReturnsTheBrowserWithItsCodeAsync`;
  - Also:
    - `AppPasswordIdTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `ConfigurationKeyTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `DocumentNameTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_ABodyIsNamedWhereEveryDeclaredValueReadsAsync`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AnEndpointDeclaresEachCarriedCodeOnce`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_AnEndpointDeclaresEachCodeOnce`;
    - `EndpointDeclarationTests.CONV_DESIGN_006_TheFirstDeclaredValueThatDoesNotParseIsTheOneNamedAsync`;
    - `ResourceIdTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `ResourceTypeTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `RestrictionNameTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `RoleNameTests.CONV_DESIGN_006_TheValueIsReadFromTheTextOfARoute`;
    - `SignInFlowTests.CONV_DESIGN_006_AReplayedCodeIsAnsweredAtAStepUpAndAtASignInAsync`;
- CONV-ERR-003:
  - AC1:
    - `SwallowedExceptionAnalyzerTests.CONV_ERR_003_AC1_ASwallowedExceptionIsAnErrorInTheBuildAsync` (at base);
  - AC2:
    - `LibraryStructureTests.CONV_ERR_003_AC2_NoCatchButTheOneAroundAfterCommitWorkCarriesOn`;
- CONV-SETUP-001:
  - AC1:
    - `LibraryStructureTests.CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties` (at base);
  - AC3:
    - `LibraryStructureTests.CONV_SETUP_001_AC3_TheSdkIsOfTheTargetedReleaseAndRollsForwardByPatch`;
- IDN-MEM-003:
  - AC1:
    - `ModelTests.IDN_MEM_003_AC1_NoSessionColumnNamesAnOrganization`;
  - AC2:
    - `GateBehaviourTests.IDN_MEM_003_AC2_TheCheckTakesTheOrganizationFromTheResourceAsync`;
- IDN-ORG-003:
  - AC1:
    - `OrganizationDirectoryTests.IDN_ORG_003_AC1_OnlyCurrentMembersAreNamedAsync` (at base);
    - `OrganizationEndpointTests.IDN_ORG_003_AC1_ADeletionRequestEndsEveryMemberSessionAsync` (at base);
  - AC2:
    - `OrganizationErasureSweepTests.IDN_ORG_003_AC2_ACancelledWindowIsNotReachedByThePassAsync` (at base);
    - `OrganizationErasureSweepTests.IDN_ORG_003_AC2_AWindowCancelledMeanwhileIsLeftBeAsync`;
    - `OrganizationStatesTests.IDN_ORG_003_AC2_ACancellationAndTheErasureAtOnceDoNotBothStandAsync`;
    - `OrganizationStoreTests.IDN_ORG_003_AC2_ACancellationClearsTheWindowOnTheRowAsync` (at base);
    - `OrganizationTests.IDN_ORG_003_AC2_CancellingInsideTheWindowRestoresTheOrganization` (at base);
  - AC3:
    - `OrganizationErasureSweepTests.IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapsesAsync` (at base);
    - `OrganizationStatesTests.IDN_ORG_003_AC3_AnErasureBeforeTheWindowElapsesWritesNothingAsync` (at base);
    - `OrganizationTests.IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapses` (at base);
  - AC4:
    - `OrganizationTests.IDN_ORG_003_AC4_TheWindowIsTheConfiguredOne` (at base);
  - AC5:
    - `OrganizationStatesTests.IDN_ORG_003_AC5_TheRowSurvivesAndTheNameBecomesTheIdentifierAsync` (at base);
    - `OrganizationStoreTests.IDN_ORG_003_AC5_TheRowSurvivesTheErasureAsync` (at base);
    - `OrganizationTests.IDN_ORG_003_AC5_TheOrganizationSurvivesItsErasure` (at base);
  - AC6:
    - `OrganizationErasureSweepTests.IDN_ORG_003_AC6_AnErasureWhoseEventRowFailsErasesNothingAsync`;
  - AC7:
    - `OrganizationErasureSweepTests.IDN_ORG_003_AC7_APassThatCannotReadItsWindowErasesNothingAsync`;
  - AC8:
    - `GateBehaviourTests.IDN_ORG_003_AC8_ASuspendedOrganizationConfersNothingAsync`;
    - `OrganizationEndpointTests.IDN_ORG_003_AC8_TheRequestEndsEveryMemberSessionAndTheMemberSignsInAgainAsync`;
    - `OrganizationServiceTests.IDN_ORG_003_AC8_TheMemberSessionsEndInTheUnitOfWorkThatSuspendsAsync`;
  - AC9:
    - `OrganizationEndpointTests.IDN_ORG_003_AC9_ACancellationRestoresNoEndedSessionAsync`;
  - AC10:
    - `OrganizationEndpointTests.IDN_ORG_003_AC10_ACancellationAtOrAfterTheWindowsEndIsRefusedErasedOrNotAsync`;
  - AC11:
    - `GateBehaviourTests.IDN_ORG_003_AC11_ASuspendedOrganizationsDerivationIsExplainedAsADenialAsync`;
    - `GateBehaviourTests.IDN_ORG_003_AC11_ASuspendedOrganizationsDerivationsConferNothingAsync`;
    - `MaterialisationTests.IDN_ORG_003_AC11_AMaterialisedDerivationConfersNothingWhileDeletionIsRequestedAsync`;
  - AC12:
    - `InvitationServiceTests.IDN_ORG_003_AC12_ADeletionRequestedMeanwhileTakesNoInvitationAsync`;
  - AC13:
    - `OrganizationEndpointTests.IDN_ORG_003_AC13_AnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`;
  - Also:
    - `OrganizationDirectoryTests.IDN_ORG_003_AChangeIsRecordedUnderTheOrganizationAsync` (at base);
    - `OrganizationEndpointTests.IDN_ORG_003_ASecondRequestChangesNothingAsync` (at base);
    - `OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsAnnouncedWithWhatItEndedAsync` (at base);
    - `OrganizationErasureSweepTests.IDN_ORG_003_TheErasureIsWrittenDownAsync` (at base);
    - `OrganizationPolicyEndpointTests.IDN_ORG_003_ThePolicyOfAnOrganizationTheDeploymentDoesNotHoldIsNotFoundAsync`;
    - `OrganizationStatesTests.IDN_ORG_003_TheErasureEndsEveryCurrentMembershipAsync` (at base);
    - `OrganizationStatesTests.IDN_ORG_003_TheWindowsThatHaveRunOutAreWhatThePassReadsAsync` (at base);
    - `OrganizationStoreTests.IDN_ORG_003_TheErasureWritesNoKeyOfTheNameItReplacedAsync` (at base);
    - `OrganizationTests.IDN_ORG_003_ADeletionRequestSuspendsTheOrganizationAtOnce` (at base);
    - `OrganizationTests.IDN_ORG_003_TheErasureLeavesNoKeyOfTheNameItReplaced` (at base);
- OPS-OBS-001:
  - AC1:
    - `ExplanationTests.OPS_OBS_001_AC1_ADenialResolvesToThePermissionAndTheMissingGrantAsync` (at base);
  - AC2:
    - `ExplanationTests.OPS_OBS_001_AC2_OnesOwnRefusalIsExplainedWithoutAnElevatedRoleAsync` (at base);
- PRIV-RIGHT-004:
  - AC1:
    - `PrivacyRequestTests.PRIV_RIGHT_004_AC1_FulfillingARestrictionRestrictsTheAccountAsync` (at base);
  - AC2:
    - `AccountAdministrationEndpointTests.PRIV_RIGHT_004_AC2_ARestrictionIsLiftedAsync` (at base);
    - `AccountAdministrationTests.PRIV_RIGHT_004_AC2_LiftingARestrictionRestoresTheAccountAsync` (at base);
    - `AccountDirectoryTests.PRIV_RIGHT_004_AC2_TheLiftTellsTheSubscribersAsync` (at base);
    - `GateBehaviourTests.PRIV_RIGHT_004_AC2_LiftingTheRestrictionRestoresWhatWasThereBeforeAsync` (at base);
  - AC3:
    - `GateBehaviourTests.PRIV_RIGHT_004_AC3_EveryWayOfAskingTheGateCarriesTheRestrictionAsync` (at base);
    - `GateBehaviourTests.PRIV_RIGHT_004_AC3_ADeclaredJobIsAdmittedToModifyARestrictedSubjectsRecordAsync`;
  - AC4:
    - `AccountStatesTests.PRIV_RIGHT_004_AC4_ARestrictionIsHeldThroughATakedownAsync`;
    - `AccountTests.PRIV_RIGHT_004_AC4_ARestrictionDecidedAwayFromActiveIsHeld`;
    - `AccountTests.PRIV_RIGHT_004_AC4_ARestrictionIsHeldThroughADeletionWindow`;
    - `AccountTests.PRIV_RIGHT_004_AC4_ARestrictionIsHeldThroughASuspension`;
  - AC5:
    - `AccountAdministrationTests.PRIV_RIGHT_004_AC5_ARestrictionHeldThroughADeletionWindowIsNotLiftedAsync`;
    - `AccountAdministrationTests.PRIV_RIGHT_004_AC5_LiftingARestrictionAsksForStepUpAsync`;
    - `AccountAdministrationTests.PRIV_RIGHT_004_AC5_OnlyARestrictionInForceIsLiftedAsync`;
  - Also:
    - `PrivacyRequestTests.PRIV_RIGHT_004_ARestrictionFulfilledWhileSuspendedIsHeldAsync` (at base);
    - `SubjectEraserTests.PRIV_RIGHT_004_AnErasedAccountHoldsNoRestrictionAsync` (at base);

**Criteria no test decides**

- API-CAP-001 AC3 and AC4, AUTHZ-GATE-005 AC4: frontend criteria, proven with the
  frontends (plan section 4, step 10).
- FE-COMP-006 AC1b and AC3: frontend criteria; their server side is proven by the tests
  under `09`, `10` and `18` above, and the frontend side with the frontends (plan
  section 4, step 10).
- PRIV-CONS-006a AC2: frontend behaviour, proven with the frontends (plan section 4,
  step 10); the server's half, the notice's cookie list, is AC1's tests.
- OPS-SEC-001:
  - AC1: the full-history secret scan (OPS-DEP-004) over every commit (section 5) and the
    "Secret scanning" job; the image layers are proven in Milestone 2.
  - AC3: a host criterion, proven by the host (plan section 4).
- CONV-DEP-001 AC1 and AC2: the "Locked restore" job, a `--locked-mode` restore of every
  project of the library's solution against its committed lockfile, which fails where
  the resolved set differs.
- CONV-SETUP-002 AC2: the same job.
- CONV-DESIGN-002 AC2 and CONV-DESIGN-008 AC2: the "Dependency allow-list" job, which runs
  `CONV_DESIGN_008_AC1_ThePackageSetIsExactlyTheAllowList`; no dispatch, behaviour or
  mapping package is in the table.
- CONV-DESIGN-006 AC2: verified by review of every endpoint mapping in
  `src/Janus.Hosting`; no job decides it.
- CONV-GATE-001:
  - AC1: `.github/workflows/gates.yml` runs on `pull_request`; the pull request run on
    the last commit pushed ran every job (section 5).
  - AC2: met. The protection of `main` requires the status checks of every job the
    criterion lists, 23, the sample host's job among them (added under D-210); read back
    from the branch protection's required checks.
- CONV-LAYOUT-001 AC1: `LIB_PKG_001_AC1_NoAreaIsCompiledAgainstAnotherAreasInternals` and
  the "InternalsVisibleTo allow-list" job.
- CONV-LAYOUT-002 AC2: the "Public surface files up to date" job, over the `PublicAPI`
  files of `Janus.Core`, `Janus.Hosting`, `Janus.Conformance` and `Janus.Testing`.
- CONV-SETUP-001 AC2: `TreatWarningsAsErrors` in `Directory.Build.props`, which
  `CONV_SETUP_001_AC1_NoProjectOverridesTheInheritedProperties` holds every project to;
  every build job fails on a warning.
- CONV-SETUP-003 AC2: `release.sh` in the "Public surface files up to date" job, which
  fails a range that changes a `PublicAPI.Shipped.txt` outside a release commit.
- CONV-SETUP-004 AC1: the "Format" job.
- OPS-DEP-004 AC7: verified by running the changed script against repositories built at
  run time, as D-211 answer 51 decides (section 1, under D-210 and D-211).
- CONV-TEST-001 AC1: verified by review of the tree: each test project is named for its
  source project and mirrors its folders; no job decides it.
- CONV-VCS-004 AC1: `truth-table-change.sh` in the "Truth-table suite" job.
- BFF-HOST-002 AC6, a Razor page reached through `MapFallbackToPage`: verified in review,
  as D-215 answer 54 decides. `SessionRequired.Asks` reads the metadata of the endpoint
  routing selects whatever its type, so a page's endpoint carrying no mark is held to a
  session, and `AllowNoSessionAttribute` on the page is read as on an action, the nearer
  mark standing; `SessionRequirementPolicy.AppliesToEndpoints` answers true where the
  candidates hold a dynamic endpoint, which a page fallback is. The controller tests
  under D-211 prove the rule.

## 2. Items in the round not implemented

- **LIB-PKG-001 AC3 and LIB-PKG-003 AC1:** proven in plan section 4, steps 1 and 2.
- **LIB-API-001 AC1:** known issue 45 of the fourth corrections report (what
  "documented" asks of the repository).
- **CONV-DEP-004:** carried into Milestone 2 as a known issue (plan section 4); no code.
- **`11` section 5, `13` R-M20, `15` sections 3.7 and 6.3, and `19` INF-HOST-003:**
  documentation, runbook and infrastructure; no code in this round. INF-HOST-003's
  `migrate` document is proven by OPS-MIG-001 AC4.
- **IDN-MEM-001 AC7, the end at the job's first pass after its instant, and `19`
  INF-BG-001's `membership-ends`:** parked on Q57 (section 4). The end set in advance,
  its move and its cancellation are built.
- **`13` R-M23, `15` section 3.5 and `16` sections 2 and 3:** documentation; no code.

## 3. Resolved by rule

1. Moved to section 4 as Q44: no Tier 1 (D-209).
2. **`tests/Janus.Storage.Tests/Migrations/MigrationRunTests.cs`.**
   - Out of step: the test ran `dotnet ef database update` as "the migration tool the
     pipeline runs".
   - Governing: OPS-MIG-001 (D-204) makes that step `migrate`.
   - Rule: the case moved to `tests/Janus.Cli.Tests/MigrateTests.cs` with the tool it
     runs, and its assertion and identifier changed with the tool, to OPS-MIG-001 AC2,
     which it proves.
3. **`tests/Janus.Hosting.Tests/Deployment.cs`.**
   - Out of step: the test deployment could not map host endpoints after the browser
     profile.
   - Governing: the working guide's section 3 (test infrastructure), BFF-HOST-001 to BFF-HOST-004.
   - Rule: it takes an optional mapping of host endpoints and registers `IRequestAccess`
     as `AddJanus` does. No runtime code changed.
4. **`tests/Janus.Hosting.Tests/Bff/EndpointAnswersTests.cs`.**
   - Out of step: its fixture built an endpoint without the library's group mark.
   - Governing: BFF-HOST-002, "the library's own endpoints keep their own marks".
   - Rule: the fixture now builds the endpoint as `MapIdentityEndpoints` mounts it.
5. **`LibraryStructureTests.IsCipherOrStore`.**
   - Out of step: it admitted only `*Store` files.
   - Governing: CONV-DESIGN-003 (D-203), under which the `IPersonalFields`
     implementation in Storage calls the same cipher.
   - Rule: `PersonalFields.cs` and `LentFields.cs` are admitted beside the port
     implementations.
6. **`PublicSurfaceTests.NotServiceContracts`.**
   - Out of step: it lacked `IExportContributor`.
   - Governing: LIB-API-001, which lists it among the extension contracts a host
     implements.
   - Rule: an interface a host implements is not a service contract.
7. **`DeclaredProcessingTests.PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis`.**
   - Out of step: the test did not cover `DeliveryKind`.
   - Governing: D-203, which made `DeliveryKind` a wire vocabulary.
   - Rule: the file list includes `DeliveryKind.cs`, and the test asserts its wire value
     as it does the others. `Wire` became generic over the enumeration.
8. **`PrivacyContractTests`.**
   - Out of step: it asserted that no public cookie type exists.
   - Governing: D-203, which retired that assertion.
   - Rule: the assertion is dropped, and the test is renamed
     `PRIV_CONS_006a_TheLibrarySetsOnlyTheFiveNecessaryCookies` for what it still proves.
9. **`tests/Janus.Hosting.Tests/endpoints.txt`.**
   - Out of step: the nullable `purpose`, and a cookie's `purpose` and `lifetime`, were
     rendered with a `?`.
   - Governing: LIB-TEST-002, the contract list being the generator's output, which
     writes no `?` on a string.
   - Rule: they render as `string`.
10. Moved to section 4 as Q45: no Tier 1 (D-209).
11. **`LibraryStructureTests.LIB_API_002_AC2_TheConformanceSampleTakesItsSchemaFromMigrate`.**
    - Out of step: its list of the conformance sample's references named `Janus.Cli` and
      `Janus.Conformance` only.
    - Governing: D-206 answer 19 and LIB-API-002 AC2, under which the sample enrols with
      `Janus.Testing`'s `SoftwarePasskey`.
    - Rule: a sample that enrols with the testing package references it, so the list names
      `Janus.Testing`.
12. **Test project references.**
    - Out of step: `Janus.Conformance.Tests` did not reference `Janus.Testing`, and
      `Janus.Testing.Tests` had no host to sign its people in to.
    - Governing: the working guide's section 3 (test infrastructure), CONV-TEST-001.
    - Rule: `Janus.Conformance.Tests` references `src/Janus.Testing` and
      `Janus.Testing.Tests` references `Janus.Conformance.Tests` for its sample host; both
      lockfiles were regenerated, and no grant was added.
13. **`FactorCatalogueTests.AUTH_PRIN_002_AC1_NoEnumFlagOrClaimDistinguishesStaffFromCustomers`.**
    - Out of step: its text scan names `Janus.Testing/TestPeople.cs` (`CustomerAsync`,
      `StaffMemberAsync`).
    - Governing: AUTH-PRIN-002 AC1 forbids an enum, flag or claim telling staff from
      customers; LIB-TEST-003 names the customer and the staff member `TestPeople` makes.
    - Rule: a way of signing a test's person in is no property of a principal, so the list
      names the file beside `TakedownTrigger.cs`.
14. **`.gitleaks.toml` (`4d4b7e67`).**
    - Out of step: the full-history scan flagged `PRIV-CONS-006a` in the documentation
      comment of `src/Janus.Hosting/DeclarationCoverage.cs`, in `5274d16b` and
      `7a21d60e`, as a generic key.
    - Governing: OPS-DEP-004 and the working guide's section 3, for a flagged value that
      is an item identifier.
    - Rule: one entry, the file and the exact value `PRIV-CONS-006a` both required to
      match, as the existing entries are.
15. **`tests/Janus.Conformance.Tests/SampleHost.cs`, `Built` (`b3702a76`).**
    - Out of step: the deployment the testing package's tests run cleared every logging
      provider, so a fault in it named nothing.
    - Governing: D-207's observations and the working guide's section 3 (test
      infrastructure).
    - Rule: the fixture keeps its cleared providers and adds the shared framework's
      console provider at the Error level; no runtime code and no shipped surface changed.
16. **`tests/Janus.Authentication.Tests/Sending/PhoneSignalAuditInMemory.cs` and
    `tests/Janus.Hosting.Tests/Deployment.cs` (`3ccfb35c`).**
    - Out of step: the fake audit could not show a `risk` record standing apart from a
      unit of work rolled back.
    - Governing: CONV-DESIGN-003 AC13 and the working guide's section 3 (test
      infrastructure).
    - Rule: the fake takes the in-memory unit of work and undoes its record on rollback;
      test fixtures only.
17. **`OrganizationDomainService.AddDomainAsync` of a domain already listed, and
    `RemoveDomainAsync` of one not listed (`3ccfb35c`).**
    - Out of step: these successes committed an empty unit of work once the step-up moved
      before it (D-207 answer 30).
    - Governing: CONV-DESIGN-003, under which a success that wrote nothing rolls back.
    - Rule: they answer success before any unit of work begins, and one seen under the
      lock rolls back and answers success; no answer changes.
18. **`PublicSurfaceTests.GateSteps` (`4ca48d14`).**
    - Out of step: the list of gate steps lacked `IGrantAdmission`.
    - Governing: D-208 answer 35 and CONV-DESIGN-007.
    - Rule: a gate step D-208 introduces is listed beside the other gate steps.
19. **`tests/Janus.Hosting.Tests/Authorization/GrantAdmissionInMemory.cs`, a
    non-recording `Admits` on `AccessGateInMemory`, and its registration in
    `tests/Janus.Hosting.Tests/Deployment.cs` (`4ca48d14`).**
    - Out of step: the in-memory deployment resolved no grant admission, so the
      AUTHZ-SCOPE-001 AC4 endpoint test could not exist.
    - Governing: the working guide's section 3 (test infrastructure).
    - Rule: the deployment registers a fake for every gate step the endpoints resolve;
      test fixtures only.
20. **`ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint`
    (`68aaf9ec`).**
    - Out of step: in a linked checkout the scan read the `.git` file at the root, which
      names the clone's path, and failed on it.
    - Governing: CONV-NAME-001, whose rule covers the sources, never git's own metadata
      (a gate that mis-implements its rule).
    - Rule: the scan leaves out a root file named `.git`.
21. **`BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut` and
    `BrowserProfileTests.BFF_CSRF_001_AC2_NoEndpointCanBeExcludedByConfigurationOrAttribute`
    (`0f449a3e`).**
    - Out of step: their lists of the sources that read an endpoint's metadata lacked
      `src/Janus.Hosting/Bff/SessionRequirementPolicy.cs`.
    - Governing: D-209, which holds stage 8 through a matcher policy (BFF-HOST-002,
      BFF-ORDER-001).
    - Rule: the matcher policy D-209 introduces is listed beside the stage that reads the
      same mark.
22. **`.github/gates/sample-host.sh` (`1cfa788e`).**
    - Out of step: the package source mapping the review asks for, `Janus.*` from the
      feed, is a pattern that `CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint`
      refuses in a source.
    - Governing: CONV-LAYOUT-001 AC5, which names the packages the library packs, and
      LIB-TEST-004.
    - Rule: the mapping names the five packed identifiers (`Janus.Hosting`,
      `Janus.Testing`, `Janus.Cli`, `Janus.Conformance`, `Janus.Analyzers`) for the feed
      and `*` for nuget.org, which takes every one of the library's packages from the
      feed alone.
23. **`DELETE /admin/roles/{name}`, judged for reach (`60734c0d`).**
    - Out of step: AUTHZ-GRANT-005 holds "a change to a role" to its actor's reach, and
      its criterion 4 names a definition alone; whether a removal is such a change is
      not said there.
    - Governing: `09` section 8, `GET|POST|DELETE /admin/roles`, which calls every
      change, the `DELETE` included, a change carrying a reason, and says a change stays
      within the actor's reach, the permissions the role carries before and after.
    - Rule: a role's removal is judged as a change whose "after" carries nothing, so the
      permissions and `handsOut` the role carried before are held to the actor's reach.
24. **The permission an acknowledgement's `authz.grant.self` refusal is recorded under
    (`eb4ea421`).**
    - Out of step: AUTHZ-GRANT-005 refuses the acknowledgement of an invitation its
      acknowledger issued, and names no permission for the refusal's record.
    - Governing: AUTHZ-GRANT-005 and AUTHZ-CONCEAL-004, which record every hand-out
      refusal under the permission its change is admitted under, and IDN-MEM-001 and
      REG-INV-001, which govern a membership by `membership:manage`.
    - Rule: the refusal is recorded under `membership:manage`, the permission an
      acknowledgement is admitted under.

## 4. Open questions

**Answered.**

- **Q1 (Tier 2).** NU1902 and NU1903 on `System.Security.Cryptography.Xml` and `.Pkcs`
  10.0.11, which reach the two web test projects transitively through OpenIddict. This
  parked every commit.
  - Answered by D-205: the test projects take the shared framework.
- **Q2 (Tier 2).** The main package: which project, which identifier, and how the area
  assemblies are packed. This parked the packing, LIB-TEST-004 and `TestDeployment`.
  - Answered by D-205: `Janus.Hosting` packs the area assemblies, and the companions
    depend on it.
- **Q3 (Tier 3).** Where `RequestAccess.Assurance.Reachable` comes from, given that stage
  5 reads only the session. This parked BFF-HOST-001 and LIB-API-001's row for it.
  - Answered by D-205.

- **Q4 (Tier 3). `System.Formats.Cbor`.**
  - The two rules disagree:
    - CONV-DESIGN-008 has `Janus.Testing` reference it directly;
    - CONV-LAYOUT-001 and its AC4 say a package the shared framework serves resolves
      from the framework. Framework 10.0 serves it, and NuGet raises NU1510 on the
      reference.
  - Parked:
    - the reference;
    - its `Directory.Packages.props` row;
    - its allow-list entry.
  - Answered by D-206 answer 4; built (section 1).
- **Q5 (Tier 2). `TestDeployment`'s `register-client` arguments.**
  - The command needs `--name`, `--kind`, `--redirect` and `--scopes`. `SignOnClient`
    gives a client identifier only.
  - Parked:
    - `TestDeployment`, with `TestPeople` and `TestSession`;
    - LIB-TEST-003 AC1, AC3 and AC5;
    - LIB-TEST-004;
    - the sample host's row of CONV-GATE-001.
  - Answered by D-206 answer 5; built (section 1). LIB-TEST-004, which Q33 and Q34 held,
    is built under D-208 (section 1).
- **Q6 (Tier 2). `migrate --report`.**
  - OPS-MIG-001 states neither its output form nor its exit code where a destructive
    statement is found.
  - Parked: OPS-MIG-001 AC5.
  - Answered by D-206 answer 6; built (section 1).
- **Q7 (Tier 2). `UseUnitOfWorkAsync`, "a context that holds a connection of its own".**
  - Readings:
    - **A:** an open connection with its own transaction;
    - **B:** any context configured with a connection or a connection string.
  - The signature is not stated either.
  - Parked:
    - AUTHZ-INHERIT-002 AC1's host row;
    - `UseUnitOfWorkAsync`;
    - the kept-write fault.
  - Answered by D-206 answer 7; built (section 1).
- **Q8 (Tier 2). The export's machine format.**
  - Readings:
    - **A:** flat, with built `section.index.value` keys;
    - **B:** "records, each an object of its named values".
  - Keys can collide under A.
  - Parked: PRIV-RIGHT-003 AC5's machine half.
  - Answered by D-206 answer 8; built (section 1).
- **Q9 (Tier 2). The name rule on an answered section's or value's name.**
  - `PlaceName` is internal to `Janus.Core`, and `Janus.Privacy` holds no grant to it.
  - Parked: that fault.
  - Answered by D-206 answer 9; built (section 1).
- **Q10 (Tier 2). `details.declaration` for an export contributor's name fault.**
  - Readings: **A:** the bare name; **B:** `contributor.<name>`.
  - Parked: the contributor name startup checks.
  - Answered by D-206 answer 10; built (section 1).
- **Q11 (Tier 3). Two encrypted fields of one type under one name.**
  - Their values can be swapped undetected, and no refusal is stated.
  - Nothing built.
  - Answered by D-206 answer 11; built (section 1).
- **Q12 (Tier 2). `CapabilityFields`.**
  - The `can` and `requires` keys could be the action or the permission identifier.
  - The return type is not stated.
  - Parked:
    - `CapabilityFields`;
    - BFF-CAP-002 AC3;
    - BFF-ORDER-001 stage 10's check.
  - Answered by D-206 answer 12; built (section 1).
- **Q13 (Tier 3). BFF-HOST-001 AC5.**
  - It asks for a new session identifier, against the `SessionId` kept at rotation.
  - Parked: AC5.
  - Answered by D-206 answer 13; built (section 1).
- **Q14 (Tier 2). `AddJanus` clears the HTTP JSON `TypeInfoResolverChain`.**
  - A host endpoint's own types then fail to serialize.
  - Parks nothing built. The sample host meets it.
  - Answered by D-206 answer 14; built (section 1).
- **Q15 (Tier 2). A job principal refused "while a request is being served".**
  - No mechanism tells a job's run from a request.
  - Parked:
    - `Job` with its refusals and its admission at the gate;
    - IDN-PRIN-001 AC6 and AC7;
    - OPS-ALERT-006;
    - INT-SMS-003's job part.
  - Answered by D-206 answer 15; built (section 1).
- **Q16 (Tier 2). The held commits `78ae1925` and `bd1b5da0`.**
  - `78ae1925` carries a 73-character body line that the commit-message gate
    (CONV-VCS-003) fails, and no commit may be rewritten.
  - It also writes `authz.grant.revoked` with subject none, which is reading (b) of known
    issue 1, still parked.
  - Parked: the items of section 2 they hold.
  - Answered by D-206: the body amended, known issue 1 built as D-206 answers it, and the
    branch merged (`727e76d7`); built (section 1).
- **Q17 (Tier 2). `10` 5.29's command principal `migrate`.**
  - Section 5.29 makes a principal the name written on the audit rows a command writes.
    `migrate` writes none, calls no gate and raises no job failure.
  - Readings, with the smallest fix for each:
    - **A:** a row of 5.29 only, with no code object;
    - **B:** `migrate` writes an audit row naming it (a new action and row);
    - **C:** an unused `SystemPrincipal` constant held by `LibraryMigrations`.
  - Parked: the principal as a code object.
  - Answered by D-206 answer 17; built (section 1).
- **Q18 (Tier 2). The commands' contract list.**
  - The criteria (LIB-API-001 AC2, LIB-TEST-002 AC1, REF-001 AC2) name no data source and
    no form for the input and output entries. Nothing declares a command's input or
    output.
  - Readings, with the smallest fix for each:
    - **A:** each command declares its contract as an internal value, and the test renders
      it into a committed file that `release.sh` reads;
    - **B:** the arguments are rendered from each parser, and the input, output and exit
      entries are written by hand;
    - **C:** a hand-kept file the test holds to the command names only.
  - Parked:
    - the list;
    - its test;
    - its place in `release.sh`.
  - Answered by D-206 answer 18; built (section 1).
- **Q19 (Tier 2). LIB-API-002 AC2, "its configuration through the library's
  operations".**
  - The sample sets `policy.default`, an R key, and none of the operations can write it
    at the sample's start:
    - `configure` takes protected keys only;
    - `bootstrap` takes the required values only;
    - `IConfigurationAdministration.ChangeAsync` needs a `config:manage` person's context
      and session, and nobody is signed in at the sample's start.
  - Readings, with the smallest fix for each:
    - **A:** the sample enrols the bootstrap administrator and calls `ChangeAsync` as
      them;
    - **B:** `bootstrap` admits `--policy.default`;
    - **C:** `configure` takes R keys.
  - Parked: the replacement of the sample's write to `identity.settings`.
  - Answered by D-206 answer 19; built (section 1).
- **Q20 (Tier 3). Whose restriction applies to a host job's context.**
  - IDN-PRIN-001 admits a job "where the record's subject is restricted from nothing", and
    a job's context names no acting account.
  - Readings: **A:** the restriction of the record's data subject; **B:** the acting
    account's, so none.
  - Parked:
    - a job's admission for its declared permissions (check, filter, fragment);
    - its refusal where the subject's deletion is requested or the subject is erased,
      and "refused for any other";
    - the truth-table row "admitted outside a request";
    - IDN-PRIN-001 AC6's admission clauses and OPS-ALERT-006's admitted job;
    - LIB-TEST-004 AC2's job clause.
  - Answered by D-207 answer 20; built (section 1).
- **Q21 (Tier 3). The org-wide `RequireAsync` and `CapabilitiesAsync` for a job's
  context.**
  - Neither the item nor the gate says how a job's context answers the org-wide question.
  - Parked: both; the org-wide check refuses a job's context.
  - Answered by D-207 answer 21; built (section 1). A job's consent step, which Q38
    held, is proven under D-208 (section 1).
- **Q22 (Tier 3). `ExplainAsync` and `WhoCanAccessAsync` for a job's context.**
  - Parked: both for a job's context.
  - Answered by D-207 answer 22; built (section 1).
- **Q23 (Tier 3). `IDerivationMaterialiser` with a job's context.**
  - The granter, the reason and the principal of what it writes are not stated.
  - Parked: the materialiser for a job's context.
  - Answered by D-207 answer 23; built (section 1). D-208 confirmed the refresh's reading
    within the limits a system principal has; a refresh asked while a request is served,
    which Q39 held, and those limits are built under D-208 (section 1), and the limit
    D-209 adds under D-209.
- **Q24 (Tier 2). `UseUnitOfWorkAsync` on a context configured with a connection object
  it owns.**
  - EF Core 10's `SetDbConnection` disposes a connection the context owns when it replaces
    it, so the context cannot return to "the connection it was configured with".
  - Readings, with the smallest fix for each:
    - **A:** refuse it with `InvalidOperationException` at the call (one condition, one
      test, a clause in AC6);
    - **B:** admit it, and state that such a context is unusable once the unit of work
      ends (one sentence).
  - Parked: that configuration; it joins today, and throws `ObjectDisposedException` at
    its next use. No test covers it.
  - Answered by D-207 answer 24; built (section 1).
- **Q25 (Tier 3). A joined context returned to a `DbContext` pool before its unit of work
  ends.**
  - A host that ends its scope without committing or rolling back can return the joined
    context to the pool while the unit of work is open, and the pool's reset keeps the
    connection, so another lease could run on it inside the transaction.
  - Parks nothing.
  - Answered by D-207 answer 25; built (section 1): a pooled context is refused.
- **Q26 (Tier 2). The public signatures of `TestDeployment`, `TestPeople` and
  `TestSession`.**
  - LIB-TEST-003 states what they do and not their signatures; those built are in
    `src/Janus.Testing/PublicAPI.Unshipped.txt`. The customer's date of birth (1990-01-01)
    and the passkey's label ("Software passkey") are not stated either.
  - Parks nothing.
  - Answered by D-207 answer 26; the values are proven (section 1).
- **Q27 (Tier 2). A fault in a `Janus.Cli` command that is not a refusal.**
  - OPS-SEC-001 states the refusal line and exit 1, and `migrate` writes `system.fault`
    for a database error; any other exception escapes `Program.RanAsync` with no line.
  - Readings, with the smallest fix for each:
    - **A:** every command writes `{"code":"system.fault"}` and exits 1 (one catch, one
      test);
    - **B:** a fault is not a refusal and may end the process (a sentence in `06`).
  - Parks nothing.
  - Answered by D-207 answer 27; built (section 1).
- **Q28 (Tier 3). A refresh token's reuse commits its kept writes in three
  transactions.**
  - `TokenReuse` revokes the grant's tokens and the grant, each committing alone, before
    `OidcService.ReuseAsync` ends the session family and writes the audit record in a unit
    of work of its own; CONV-DESIGN-003 commits a refusal's kept writes together.
  - Parks nothing; `TokenReuse` is unchanged.
  - Answered by D-207 answer 28; built (section 1). A reused token carrying no session
    or no subject claim, which Q40 held, is built under D-208 (section 1).
- **Q29 (Tier 2). "A rejected callback's count and record" (INT-SMS-005).**
  - The callback guards and the provider-event intake write the rejection as the
    `CallbackLog.Refused` line; `DeliveryReports.ReportAsync` writes no record of it.
  - Readings, with the smallest fix for each:
    - **A:** the record is the log line, and the delivery report's route lacks it (log it
      there);
    - **B:** the record is a stored row (name it in `10` and write it on every route).
  - Parks nothing.
  - Answered by D-207 answer 29; built (section 1).
- **Q30 (Tier 3). The phone signal asked and its consideration recorded inside another
  operation's unit of work.**
  - `PhoneSignals.AllowsAsync` is to be asked before an operation's unit of work begins
    (CONV-DESIGN-002, D-186). The step-up guard calls it inside the caller's level: for
    example `AccountService.EditProfileAsync` begins (`AccountService.cs:206`), then
    `StepUpGuard.WithheldAsync` calls `signals.AllowsAsync` (`StepUpGuard.cs:447`), whose
    `BeginAsync` joins. A step-up refusal then rolls the consideration back. About 25
    callers of `StepUpGuard.PassedAsync` take this path.
  - Parked: the step-up's order before the unit of work in the eight operations of D-207
    answer 30, and the outermost begins it needs.
  - Answered by D-207 answer 30; built for seven of the eight operations (section 1).
    `EditProfileAsync` and the outermost begins Q30 parked are built under D-208 (Q36),
    and the outermost begin of `AllowsAsync`'s `risk` unit of work under D-209 (Q41).
- **Q31 (Tier 2). A write kept on a refusal that CONV-DESIGN-003's list does not name:
  `CredentialService.RemoveAsync`.**
  - `LossReports.SuspendAsync` commits the suspension, the report and its audit record,
    then the operation refuses `credential.last_second_factor` (AUTH-STEP-006).
  - Readings: **A:** add it to the list, so the operation begins the outermost unit of
    work; **B:** the suspension is the outcome, not a refusal's kept write (a sentence in
    CONV-DESIGN-003).
  - Parks nothing; unchanged.
  - Answered by D-207 answer 31; built (section 1).
- **Q32 (Tier 2). The same, at `RegistrationService.BeginAsync` signed in.**
  - `InvitationOpening.OpenAsync` commits the invitation's attachment, then the operation
    refuses `registration.signed_in` (REG-SESS-002).
  - Readings as Q31. Parks nothing; unchanged.
  - Answered by D-207 answer 32; built (section 1).
- **Q33 (Tier 2). The sample host's build settings and analyser scopes.**
  - The root `Directory.Build.props` sets a lockfile and MinVer, and the root
    `.editorconfig` scopes the table's relaxed rules (CA1707, CA1515, CA2007, CA1812) to
    `src/**` and `tests/**`; 08 states the sample's package versions and tool manifest
    only.
  - Readings, with the smallest fix for each:
    - **A:** the sample inherits the library's settings (a `samples/Directory.Build.props`
      importing the root one without a lockfile, MinVer among the sample's versions, the
      scopes widened, and `CONV_SETUP_004_AC3`'s list with them);
    - **B:** the sample states the same compile properties itself, with a
      `samples/.editorconfig` holding only the table's five rules;
    - **C:** as B, on the SDK's default analysis.
  - Parked: the sample's build files.
  - Answered by D-207 answer 33; built (section 1, "Built under D-208").
- **Q34 (Tier 3). No public path gives anyone a grant in an organization that
  `POST /admin/organizations` creates.**
  - AUTHZ-SCOPE-001 evaluates invitations and grants in the organization they belong to,
    so an invitation with roles needs `membership:manage` and `grant:manage` there, and
    `POST /admin/grants` needs `grant:manage` there. `POST /admin/organizations` is not
    gated and seeds no grant or membership. A grant in the administrative organization
    confers only on its members.
  - Parked: every LIB-TEST-004 AC2 test, and so the sample host's gate run.
  - Answered by D-207 answer 34; built (section 1), the sample host's tests through
    Q35's answer (section 1, "Built under D-208").

**Answered by D-208.**

- **Q35 (Tier 3). What the gate records for an alternative admission (D-207 answer 34).**
  - A grant outside the administrative organization is admitted under `grant:manage` in
    the grant's organization or `system:administer` in the administrative organization.
    Each refusal `IAccessGate.RequireAsync` gives is recorded `authz.denied` with a
    correlation identifier and counts toward `alerting.denials.threshold`
    (AUTHZ-CONCEAL-004 AC4, OPS-ALERT-006), so asking the two in turn records refusals
    for an admitted operation: up to four when a system administrator writes and revokes
    one grant (`grant:manage` asked first), or one for every ordinary grant manager
    (`system:administer` asked first), and two for one operation refused.
  - Readings: **(a)** each ask is its own check, every refusal recorded, `grant:manage`
    asked first; **(b)** one decision, recorded once only when both are refused, through
    an internal port of the gate, the chapters then naming the permission and the
    organization that record carries.
  - Answered by D-208 answer 35; built (section 1).
- **Q36 (Tier 3). The username change in `AccountService.EditProfileAsync` under D-207
  answer 30.**
  - Whether the username changes is read once without a lock and decides whether the
    step-up is asked. A concurrent change committed by the same account can make the name
    differ under the lock, and the step-up cannot be asked inside the unit of work, so the
    edit would write a username change no step-up judged.
  - Readings: **(A)** the unlocked read decides the username: a name found to differ
    under the lock writes nothing for the username and the rest commits; **(B)** the
    step-up is asked whenever an edit names a username, changed or not; **(C)** a name
    found to differ under the lock rolls back, the step-up is judged again with no unit
    of work open, and on a pass the edit begins again.
  - Parked: `EditProfileAsync`'s step-up order, and the outermost begins Q30 parked.
  - Answered by D-208 answer 36 (reading A); built (section 1), the outermost begin of
    `AllowsAsync`'s `risk` unit of work under D-209 (Q41).
- **Q37 (Tier 2). `InMemorySmsTransport.BalanceAsync` answers `decimal.MaxValue`.**
  - `identity.sms_balance_readings.balance` is `numeric(18,4)`, so every `sms-balance`
    poll on a deployment using the transport fails with `22003 numeric field overflow`.
    LIB-TEST-003 names no balance, and `abuse.sms.balancefloor` has no upper bound.
  - Readings, with the smallest fix for each (both may apply):
    - **A:** the transport answers the largest value the column holds,
      99999999999999.9999 (its documentation and `TransportTests`);
    - **B:** a reading the column cannot hold is refused or capped where it is recorded
      (`SmsBalance.cs` and the column), not a fault of the job.
  - Parked: that fix.
  - Answered by D-208 answer 37 (reading A); built (section 1).
- **Q38 (Tier 3). Whether a job's consent refusal is recorded (D-207 answer 21).**
  - Answer 21 refuses a consent-based purpose for a job "as it is for a person" and says
    every refusal is recorded as the gate records a job's. A person's
    `privacy.consent.required` writes no row today; `10` 5.24's `authz.access.denied` is
    the row a refusal's correlation identifier resolves to, answered `authz.denied`.
  - Readings: **(A)** a job's consent refusal is answered as a person's, with no row;
    **(B)** it writes an `authz.access.denied` row, and the answer carries
    `details.correlation`.
  - Parked: a job's consent step.
  - Answered by D-208 answer 38 (reading A); proven (section 1), no code changed.
- **Q39 (Tier 3). What a refresh refused while a request is served records.**
  - AUTHZ-DERIVE-005 AC7 records it as the gate records a refusal of background work. The
    gate's row carries `details.permission` and `details.resourceType`, and its
    resolution builds the AUTHZ-GATE-004 explanation, whose `permission` is required. A
    refresh asks no permission; it names a relationship and a `ResourceId`.
  - Parked: that refusal.
  - Answered by D-208 answer 39; built (section 1).
- **Q40 (Tier 3). What a reuse writes when the redeemed token carries no session or no
  subject claim (D-207 answer 28).**
  - AUTH-OIDC-003's values and `10` 5.24 give the family as spine, grant and tokens, and
    the record as written in the unit of work that revokes them, and say nothing of a
    token naming no session or subject. `TokenIssue` and `AuthorizationIssue` put the
    session claim on every token, so no endpoint reaches the path.
  - Answered by D-208 answer 40; built (section 1).

**Answered by D-209.**

- **Q41 (Tier 3). A library path reaches `PhoneSignals.AllowsAsync` inside an open unit of
  work.**
  - `IAccessGate.RequireAsync` → `AccessGate.UnsteppedAsync` → `StepUpGates.OutstandingAsync`
    → `RequestGates.JudgedAsync` → `StepUpGuard.ChallengeAsync` → `AllowsAsync`, where a
    host binds a step-up gate to a library-owned permission (for example
    `system:administer`), the session does not meet it, a texted factor is offered and a
    signal provider is declared. Nothing refuses such a binding:
    `AuthorizationDeclarationBuilder.StepUpGate` and `AuthorizationModel` check gate
    bindings only for jobs.
  - Sites asking a library permission inside their unit of work:
    `ConfigurationService.ChangeAsync` and `RetentionAsync`,
    `RestrictionAdministration.EditAsync`, `AlertDestinationChange.ChangeAsync`; under a
    race, the grant, group, role, invitation, policy and domain operations' re-asks under
    the lock.
  - AUTHZ-GATE-005's values (D-160) let a host bind its own actions, the library's
    carrying theirs in `10` section 5a, and say nothing of a host binding a library
    permission; CONV-DESIGN-003 never asks the signal inside an open transaction.
  - Readings, with the smallest fix for each:
    - **A:** a host's binding naming a library-owned permission is refused at startup
      (one check and a criterion);
    - **B:** the binding is allowed, and the operations ask the permission before their
      unit of work, or the gate's step-up judgement is cached before it (an unlocked ask
      in the four sites; the race sites stay open);
    - **C:** the `risk` unit of work begins outermost and faults when reached inside one,
      failing closed with `system.fault` on that configuration.
  - Parked: that outermost begin.
  - Answered by D-209 answer 41 (reading A, and a step-up judged while a unit of work is
    open asks no signal); built (section 1).
- **Q42 (Tier 2). The background worker's wait past `Task.Delay`'s largest delay
  (INF-BG-001).**
  - `BackgroundWorker.RunDueAsync` waits until the soonest due job among those not
    running; `Task.Delay` accepts at most about 49.7 days, so while every job still
    waiting is due later than that (a job of a long period while the short ones run),
    `ExecuteAsync` throws `ArgumentOutOfRangeException` and, under `StopHost`, the host
    stops.
  - Readings, with the smallest fix for each:
    - **A:** the wait is capped at the largest delay `Task.Delay` accepts, the loop
      running a round early and waiting again;
    - **B:** it is capped at a stated longest wait between rounds (a `10` row or a fixed
      value the chapters name).
  - Parked: that fix.
  - Answered by D-209 answer 42 (reading A); built (section 1).
- **Q43 (Tier 3). A restricted account's list filter and fragment for a modifying
  permission.**
  - `FilterAsync` and `FragmentAsync` answer a rule matching nothing (`AccessGate.cs`,
    about line 589), where the check refuses `authz.restricted`. AUTHZ-GATE-006's values
    say the gate "refuses every modifying one with `authz.restricted`" and do not say
    how a filter or a fragment answers it. No record is written either way.
  - Readings: **A:** a filter matching nothing is how a list refuses; **B:** the filter
    and the fragment refuse `authz.restricted` as the check does.
  - Parks nothing; the behaviour predates the round.
  - Answered by D-209 answer 43 (reading B); built (section 1).
- **Q44 (Tier 2, formerly section 3 record 1). `migrate`'s document with a connection
  blank or not a string.**
  - OPS-MIG-001 refused a document that "carries no connection" and said nothing of a
    connection that is blank or not text.
  - Readings: **A:** no connection, refused `model.startup.secretunavailable`,
    `details.key` `input` (the `10` row of a secret "supplied empty or unusable");
    **B:** `api.request.malformed` naming `connection`, as the other commands answer it.
  - Built as reading A in the round's first run, as a Tier 1 record.
  - Answered by D-209, "`migrate`'s blank connection" (reading B); built (section 1).
- **Q45 (Tier 2, formerly section 3 record 10). The `webauthn` member of
  `POST /auth/begin`'s 200 answer (`9216f713`).**
  - `SignInChallengeView` wrote the member `webAuthn`; `09` spells it `webauthn`. The
    change is to a wire member of an endpoint outside the round.
  - Built in the round's first run as a Tier 1 record: the member is written `webauthn`,
    with `endpoints.txt` and a changelog line.
  - Answered by D-209, "Tier 1 records": the change stands.

**Answered by D-210.**

- **Q46 (Tier 3). Stages 5 and 7 under late routing (BFF-ORDER-001, BFF-HOST-002,
  D-209).**
  - D-209 holds stage 8 through a matcher policy whatever the routing order, and says
    nothing of the earlier stages that read the endpoint. With `UseRouting` after the
    browser profile, no endpoint is selected when they run:
    - stage 5 (`SessionResolution`, through `EnrolmentRoute.Is`) resolves no enrolment
      session on an enrolment route, so such a request is refused as holding no session
      (failing closed);
    - stage 7 (`MalformedRequest`) answers `api.request.malformed` without naming the
      member that failed to parse, since the member's binding is the endpoint's.
  - Parks nothing: both fail closed.
  - Answered by D-210 answer 46: stage 5 fails closed under late routing, and routing
    before the profile serves the enrolment routes; the malformed-request refusal reads
    the endpoint routing selected whatever the order; built (section 1).
- **Q47 (Tier 2). A contributor's fault in an export is logged with its type and frames
  (review finding on `ExportService.cs`, about line 207; BFF-ERR-002 AC2, PRIV-RIGHT-003,
  CONV-LOG-003).**
  - The library logs a fault's type and frames through `FaultLog.Of`, internal to
    `Janus.Hosting` (it reads `CodedFault`, internal to `Janus.Storage`). `Janus.Privacy`
    cannot reach it, and `08` section 1a names no seam for it.
  - Readings, with the smallest fix for each:
    - **A:** an internal contract `Janus.Privacy` declares and `Janus.Hosting` implements
      over `FaultLog`, as `IExportNameRule` is (D-206); an `08` section 1a line;
    - **B:** `FaultLog` moves into `Janus.Core`, with a grant to `Janus.Privacy` (a grant
      to a shipped project);
    - **C:** the contributor's exception escapes `AssembleAsync` to the error
      translation, which logs it (the answer no longer names the contributor, and
      `IExports` throws);
    - **D:** a formatter private to `Janus.Privacy` (a second way of logging a fault).
  - Parked: that finding alone.
  - Answered by D-210 answer 47 (reading A); built under D-211 answer 50 (section 1).

- **Q48 (Tier 3). D-209's first case of a step-up inside a host's unit of work (`08`
  CONV-DESIGN-003 AC13).**
  - The case read "a host's bound action whose gate is met at the first ask and unmet at
    the ask inside its unit of work". `RequestGates` keeps each gate's judgement for the
    request, so a gate met at its first ask is not judged again inside the host's unit of
    work, and the case is not reachable end to end; it was tested at the guard alone.
  - Raised in the run under D-209 inside section 1's lines on Q41, not as a question.
  - Parked: the case's test through the gate a host asks.
  - Answered by D-210, "A bound action's step-up gate is judged once a request": the case
    cannot arise, and criterion 13's first case reads a host's bound action whose gate the
    host first asks inside its own unit of work; built (section 1).

**Answered by D-211.**

- **Q49 (Tier 2). The servicing release of 2026-10-25 (CONV-DEP-004, D-205, D-210).**
  - D-210 takes the servicing release of 2026-10-13 (.NET 10.0.13, SDK 10.0.402), built
    in `2c107c87`. Microsoft's release metadata for .NET 10 names a later one, 10.0.14 of
    2026-10-25 (SDK 10.0.403), marked a security release, and nuget.org lists
    `Microsoft.EntityFrameworkCore`, its relational and design packages and `dotnet-ef`
    at 10.0.14, published the same day.
  - Readings, with the smallest fix for each:
    - **A:** 10.0.13 stands for this round, and 10.0.14 is taken as its own change later;
    - **B:** 10.0.14 and SDK 10.0.403 are taken now, in one change over `global.json`,
      `Directory.Packages.props`, `.config/dotnet-tools.json` and the lockfiles.
  - Parks nothing: D-210's move is built as it reads.
  - Answered by D-211 answer 49 (reading B), the other Microsoft packages that follow the
    runtime with it; built (section 1), save those that reach the solution only
    transitively (Q53).
- **Q50 (Tier 2). What the fault entry of D-210 answer 47 does (`08` CONV-DESIGN-007,
  BFF-ERR-002 AC2, PRIV-RIGHT-003, CONV-LOG-003).**
  - `FaultLog.Of` builds the fault's text and logs nothing. Every other caller keeps it
    one of two ways: `ErrorTranslation`, `BackgroundWorker` and the restore test put it in
    the `system.fault` error's `fault` detail, which `Refusal.WriteAsync` logs under the
    correlation identifier and withholds from the body; `SendPublisher` logs it through
    its own log event. `Janus.Privacy` holds no logger.
  - `08` says the export keeps a contributor's fault "as the pipeline does", and D-210
    answer 47 that the export "answers as before".
  - Readings, with the smallest fix for each:
    - **A:** `IFaultEntry` answers the entry (`string Of(Exception fault)`, Hosting's over
      `FaultLog.Of`), and `ExportService` adds it as the `fault` detail beside
      `contributor`, logged by the refusal under the correlation identifier. The answer
      over HTTP is unchanged, but the error `IExports.AssembleAsync` returns gains a
      `fault` detail, so a host calling the operation directly sees it.
    - **B:** `IFaultEntry` keeps the entry in the log (`void Keep(string contributor,
      Exception fault)`), Hosting's implementation logging `FaultLog.Of` through a log
      event of its own; the operation's result is unchanged, and the entry is a second
      log line beside the refusal's, tied to the request by its scope.
  - Parked: the review finding on `ExportService.cs`, Q47's build.
  - Answered by D-211 answer 50 (reading B); built (section 1).
- **Q51 (Tier 2). A committed test of the scan's one allow-list (`06` OPS-DEP-004 AC7).**
  - Running `.github/gates/secret-scanning.sh` takes bash, git, curl, `sha256sum` and tar
    on Linux, a download of the gitleaks release, and a scratch repository holding a
    credential. The test projects are .NET projects run on Windows here and on Ubuntu in
    the pipeline; no test runs a gate script today.
  - A committed test would need what `08` does not name: a container image the tests
    pull, a test project for the pipeline's gates, a test reaching the network.
  - Readings, with the smallest fix for each:
    - **A:** no committed test; AC7 is a criterion no test decides, verified by running
      the script against scratch repositories (section 1);
    - **B:** a committed integration test, `08` naming the image, the test project and
      whether a test may reach the network.
  - Parked: the committed test alone; the script is built.
  - Answered by D-211 answer 51 (reading A); verified so (section 1).
- **Q52 (Tier 3). A host action a dynamic route resolves to holds no session
  (BFF-HOST-002 AC1 and AC5, BFF-ORDER-001 stage 8, D-202 rule 3, D-209).**
  - A host's unmarked controller action reached through `MapFallbackToController`, with
    `AllowNoSession` on the fallback, answered 200 to a browser holding no session, with
    routing before the profile and after it. The endpoint MVC resolves a dynamic route to
    is an `Endpoint` built from the controller's data, not a `RouteEndpoint`, and
    `SessionRequired.Asks` holds only a `RouteEndpoint` to a session; that line is how the
    framework's own answers (405, no match) answer as API-CONV-003 gives.
  - D-202 rule 3 and BFF-HOST-002 require a session at every endpoint the host maps after
    the profile unless it is marked, and do not say how such an endpoint is told apart
    from the framework's own answers.
  - The review's change to `SessionRequirementPolicy.AppliesToEndpoints`
    (`ContainsDynamicEndpoints`) is needed and does not decide the criterion alone.
  - Parked: that finding whole (the change, its test, its fixture), kept out of every
    commit.
  - Answered by D-211 answer 52; built (section 1), save the tests of a Razor page
    (Q54).

**Answered by D-215.**

- **Q53 (Tier 2). The Microsoft packages that follow the runtime and reach the solution
  only transitively (CONV-DESIGN-008 AC1, CONV-DEP-004, D-211 answer 49).**
  - D-211 answer 49 moves "every other Microsoft package that follows the runtime" to
    its 10.0.14 release. Two reach the solution only through a package of the table:
    `Microsoft.Bcl.Cryptography`, resolved at 10.0.2 through `Fido2`, and
    `Microsoft.Extensions.Http.Polly`, resolved at 10.0.11 through
    `OpenIddict.Validation.SystemNetHttp`.
  - Moving either needs a `PackageVersion` entry and a direct reference, or transitive
    pinning; CONV-DESIGN-008 AC1 holds the direct references to the table, and `08`
    names no pin. The vulnerability gate reports neither package.
  - Readings, with the smallest fix for each:
    - **A:** they stay where their parents put them, and D-211 answer 49 reads as the
      packages the solution references;
    - **B:** they are pinned at 10.0.14 with `CentralPackageTransitivePinningEnabled`,
      `08` naming the pins and AC1 setting them apart from the direct references.
  - Parked: the move of those two packages alone.
  - Answered by D-215 answer 53 (reading A): both stay where their parents put them;
    nothing to build.
- **Q54 (Tier 2). The tests of a Razor page under stage 8 (BFF-HOST-002, D-211 answer
  52).**
  - D-211 answer 52 and BFF-HOST-002's values name `MapFallbackToPage` and a Razor page
    marked with `[AllowNoSession]`; criterion 6 names a controller action through
    `MapFallbackToController` alone.
  - A test of a page needs the Razor SDK (`Microsoft.NET.Sdk.Razor`) on
    `Janus.Hosting.Tests` and a `.cshtml` fixture. That is not one of the changes to test
    infrastructure Tier 1 lists, and `08` does not name it.
  - Readings, with the smallest fix for each:
    - **A:** `Janus.Hosting.Tests` moves to the Razor SDK with one page fixture, `08`
      naming it: an unmarked page through `MapFallbackToPage` answers 401, and a page
      marked `[AllowNoSession]` is reached;
    - **B:** no page test: the rule reads the endpoint's metadata whatever its type,
      criterion 6 names the controller case, and the page case is verified by review.
  - Parked: the two page tests alone; the rule they would test is built.
  - Answered by D-215 answer 54 (reading B): no page test; the page case is verified in
    review (section 1, criteria no test decides).

**Answered by D-216.**

- **Q55 (Tier 2). The reason an immediate end of a membership records (IDN-MEM-001,
  D-213, `09` section 8a, `DELETE /admin/organizations/{id}/memberships/{subject}`).**
  - D-213 and IDN-MEM-001 record each revocation as `authz.grant.revoked` and each
    removal as `authz.group.memberremoved` "with the end's reason", and `10` gives
    `identity.membership.ended` a `details.reason`. The `09` row for ending a membership
    named no body, and the endpoint took none; only `PUT .../end` (`{ endsAt, reason }`)
    and `DELETE .../end` (`{ reason }`) carried a reason.
  - The code needs a reason for each revocation and removal an immediate end writes.
  - Readings, with the smallest fix for each:
    - **A:** the immediate `DELETE` takes `{ reason }`, required, as the other admin
      `DELETE`s do; a blank one is 400 `api.request.malformed` naming `reason`, and `09`
      gains the body;
    - **B:** the immediate end takes no body, and its records carry a fixed reason (for
      example `IDN-MEM-001`), as the start principals' writes do.
  - Parked: the revocations and group removals an immediate end makes, and the
    `details.reason` of its `identity.membership.ended`.
  - Answered by D-216 answer 55 (reading A); built in `739c8962` (section 1).
- **Q56 (Tier 2). `authz.handout.byholding` on a record a live derivation reaches
  (AUTHZ-GRANT-005, D-212).**
  - With the setting on, the actor's grants had to admit every permission of the role at
    the change's scope, "as the gate's grant terms decide it (... derived grants
    included)".
  - A library operation holds no host relationship sources: the library's single check
    refuses a type a live derivation reaches (`authz.derivation.sourcesmissing`), so
    library code sees stored and materialised grants and not live derived ones.
  - Readings, with the smallest fix for each:
    - **A:** the check counts stored and materialised grants alone, so it admits less
      than the rule stated, never more;
    - **B:** a change on a record of such a type is never admitted through the setting,
      only through a `handsOut` list or `system:administer`.
  - Parked: admission through a live derived grant alone; the check over stored and
    materialised grants was built.
  - Answered by D-216 answer 56 (reading A); built in `f0c28e87` with AUTHZ-GRANT-005
    AC7 (section 1).

**Unanswered.**

- **Q57 (Tier 3). Whom the audit rows of the job `membership-ends` name (IDN-MEM-001
  AC7, `10` sections 5.24 and 5.29, IDN-PRIN-001, INF-BG-002 AC2).**
  - IDN-MEM-001 has the job end a membership at its instant "as the administrator who
    set it would have, recording that administrator and reason", and its criterion 7 has
    the end "recorded with the administrator who set it"; `10` section 5.24 has
    `identity.membership.ended` name "the administrator acting or who set the end".
  - `10` section 5.29 has every background job write the audit rows it writes under its
    principal, with the principal's reason, and INF-BG-002 AC2 has a job's actions
    audited with the principal's name and the stated reason. The audit table's check
    `ck_audit_records_principal` holds a row naming a principal to the empty acting
    subject, so no row names both the job's principal and the administrator as actor.
  - The two chapters contradict each other on whom the rows of the job's pass name, for
    `identity.membership.ended` and for each `authz.grant.revoked` and
    `authz.group.memberremoved` the pass writes.
  - Parked: the job's pass (IDN-MEM-001 AC7, the end at the job's first pass after its
    instant, and `19` INF-BG-001's `membership-ends`). The principal is reserved, and
    the end set in advance, its move and its cancellation are built.

## 5. Gate result

**The run under D-212 to D-216 (D-214).**

- Fast checks on every commit from `44a3a518` to `fdd6c639`: build with warnings as
  errors, the analysers and `dotnet format --verify-no-changes` over `Janus.slnx`, and
  the unit tests of the projects each commit changes, all green; the truth-table change,
  changelog and forbidden-marker gates over each commit, green.
- The pipeline's runs on `f90c4460`, the report's commit of the first push, failed: the
  push run 40449933566 (the contract tests; the jobs after them skipped) and the pull
  request run 40449935293 (the contract tests, the truth-table suite and the
  integration tests). The causes, each fixed in a new commit:
  - `ErrorCodesTests.LIB_API_001_AC2_TheStatusesAreTheContract`,
    `SettingsCatalogueTests.LIB_API_001_AC2_TheKeysAreTheContract`,
    `EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract` and
    `SchemaContractTests.LIB_API_001_AC2_TheLibraryOwnedSchemaIsTheContractAsync`: the
    contract files (`1baea90e`);
  - `SchemaContractTests.PRIV_RIGHT_005a_AC18_EveryColumnThatCanHoldASubjectRefusesTheMaxUuidAsync`:
    `memberships.end_set_by` (`23906caa`);
  - `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync`,
    the grant's row of a role carrying system administration (`fdd6c639`).
  - After the fixes, run here as D-214 allows for a test a failing run names: the
    contract tests, 211 of 211; `SchemaContractTests`, 5 of 5; `TruthTableTests`, 203 of
    203.
- Before the push of this commit:
  - every unit test of every test project, 4742 of 4742: `Janus.Analyzers.Tests` 23,
    `Janus.Authentication.Tests` 1652, `Janus.Authorization.Tests` 235,
    `Janus.Cli.Tests` 76, `Janus.Conformance.Tests` 23, `Janus.Core.Tests` 506,
    `Janus.Hosting.Tests` 1645, `Janus.Identity.Tests` 106, `Janus.Privacy.Tests` 402,
    `Janus.Storage.Tests` 59, `Janus.Testing.Tests` 15;
  - the commit-message gate over `caba5a50` to this commit, green;
  - the full-history secret scan, gitleaks 8.30.1 as the script runs it, over every
    commit of every branch and tag: no finding.
- No full gate ran on the development machine (D-214). The pipeline's runs on this
  commit, the push run and the pull request run, are the gate of record; their
  identifiers and result are given in the run's last message.
- The pipeline's runs on `caba5a50`, the tip of the push before this run, were green:
  the push run 40418972145 and the pull request run 40418975216.

**The run under D-211.**

The pipeline's runs on `c82b09ad`, the tip of the push before that run, were green: the
push run 40285851951 and the pull request run 40285853376.

The full gate ran once on the development machine at `bd065aee`, the last commit of code,
on SDK 10.0.403 of the machine's system install, with its runtimes 10.0.14, job by job as
`.github/workflows/gates.yml` runs it, over the merge base with `main` (`b6d14fe`) to
`HEAD`:

| Job | Result |
|---|---|
| Locked restore, build, public surface, format | green |
| Unit tests | 4719 of 4719 |
| Contract tests | 211 of 211 |
| Unicode tables run and diff, tool restore | green |
| Integration tests | 1345 of 1345 |
| Policy coverage | 3 of 3 |
| Truth-table change, truth-table suite | green, 190 of 190 |
| Analyser rules, permitted outcome, forbidden log values | 23 of 23, 4 of 4, 4 of 4 |
| Dependency allow-list, internals allow-list | 2 of 2, 1 of 1 |
| Forbidden markers, acceptance test names, commit message, changelog | green |
| Destructive operations | reported only (the gate is disabled); it names the two `DELETE` statements of `OweNothingAfterAnExport` |
| Dependency vulnerabilities | green, no package with a known vulnerability |
| Double migration | both runs applied through `migrate` |
| The sample host builds from the packages packed from the same commit, and its tests pass | green, on the same SDK: `.github/gates/sample-host.sh` after a locked restore, its build with warnings as errors and `dotnet format --verify-no-changes` over `HostSample.slnx`, and its tests, 14 of 14 |

- The full-history secret scan (OPS-DEP-004) ran after the gate, gitleaks 8.30.1 as the
  script runs it (`.gitleaks.toml`, `--ignore-gitleaks-allow`, an empty ignore path, no
  baseline, no `.gitleaksignore` in the tree), over every commit of every branch and tag
  (1584 commits): no finding.
- The protection of `main` requires 23 checks, the sample host's job among them.
- That run's gate of record is the pipeline's runs on `caba5a50`, green (above).
