# Round H1: the host's way in, its privacy duties, its setup and its tests

Status: every item no question holds is built. D-206 answered Q4 to Q19, D-207 answered
Q20 to Q34 and D-208 answered Q35 to Q40, and the items they held are built (section 1,
"Built under D-206", "Built under D-207" and "Built under D-208"), the sample host among
them. Three questions (Q41 to Q43) wait on the owner, and the items they hold are listed
in section 2. The full gate result is in section 5.

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
  - `ExportServiceTests.PRIV_RIGHT_005b_AnAssembledExportOwesTheSubscribersNothingAsync`.
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
    - `MigrateTests.OPS_MIG_001_AC4_ADocumentCarryingAKeyAppliesNothingAsync`.
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
- Known issue 1 (`dd625f63`), AUTHZ-GRANT-003 AC6: `authz.grant.created`,
  `authz.grant.revoked`, `authz.grant.materialised` and `authz.grant.retracted` each name
  the grant's holder as subject where it is an account, and none where it is a group.
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
  - `PeopleTests.LIB_TEST_003_AC1_ACustomerAnAdministratorAndAStaffMemberAreSignedInAndCallTheHostsEndpointsAsync`;
  - `PeopleTests.LIB_TEST_003_AC3_AStaffMemberSignedInWithASoftwarePasskeyMeetsAPhishingResistantGateAtAal2Async`;
  - `PeopleTests.LIB_TEST_003_AC5_AStateChangingRequestThroughATestSessionPassesTheCsrfStagesAsync`;
  - `PeopleTests.LIB_TEST_003_ASessionStepsUpWithThePersonsPasskeyOrPasswordAsync`;
  - `TestDeploymentTests.LIB_TEST_003_AnAddressNoReturnAddressIsRegisteredUnderIsRefusedAsync`.
  - The library's solution installs no tool, so `TestDeployment` against the installed
    `Janus.Cli` was verified by hand: the tool packed and installed into a scratch
    manifest, `PrepareAsync` run against PostgreSQL 17, the three commands exiting 0 and
    the client registered as D-206 answer 5 states. The sample host's gate runs it.
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
- Q7, the kept-write fault (CONV-DESIGN-003, AUTHZ-INHERIT-002; `366f0e06`, `11a339bf`):
  an operation whose refusal keeps a write begins the outermost unit of work, and called
  inside an open one ends that level and throws `InvalidOperationException`, so nothing
  of the outer unit of work commits. The operations, from CONV-DESIGN-003's list:
  sign-in's factor, device code, step-up, pressed link and delegated sign-in; the second
  step's ask; break-glass; a refresh token's reuse; the bot defence; a delivery report;
  registration's begin, age, code, link and terms; an identifier's code and link; a text
  code's confirm; a provider event; the machine profile's callbacks.
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
- Q13, the session's rotation (`16c8928f`). The tests found that a sign-in completed on
  `POST /auth/factor`, `POST /auth/device/verify` or a sign-in link, and a break-glass
  presentation, cleared the pre-authentication cookie but left its record resolving; it
  now ends at every sign-in.
  - BFF-HOST-001 AC5: `RequestAccessTests.BFF_HOST_001_AC5_AStepUpKeepsTheSessionAndLeavesTheCookieBeforeItNoAccessAsync`;
  - BFF-SESS-004 AC1 and AC2: `SessionRotationTests.BFF_SESS_004_AC1_AStepUpRotatesTheIdentifierTheCookieCarriesAndKeepsTheRecordAsync`
    and `SessionRotationTests.BFF_SESS_004_AC2_TheIdentifierBeforeAStepUpIsInvalidatedAsync`;
  - AUTH-SESS-006 AC1 and AC2: `SessionRotationTests.AUTH_SESS_006_AC1_TheIdentifierAfterSignInDiffersFromTheOneBeforeAsync`
    and `..._AC2_SigningInInvalidatesTheFirstContactAsync`;
  - BFF-CSRF-005a AC3: `BreakGlassEndpointTests.BFF_CSRF_005a_AC3_PresentingTheCredentialEndsTheFirstContactAsync`.
- Q14, CONV-DESIGN-007 AC9 (`9b926a49`): `AddJanus` changes nothing of the application's
  JSON options, and every library endpoint reads its body through a library context.
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
- Q17, IDN-PRIN-001 AC3 (`1e06c928`): `SystemOperation.Migration` is removed.
  `SystemPrincipalTests.IDN_PRIN_001_AC3_TheOperationsAreThoseTheReferenceLists`.
- Q18, LIB-API-001 AC2, LIB-TEST-002 AC1 and REF-001 AC2 (`7083d7bb`): each command
  declares its contract, rendered into `tests/Janus.Cli.Tests/commands.txt`, which
  `release.sh` reads beside `endpoints.txt`.
  - `CommandContractTests.LIB_API_001_AC2_TheCommandsAreTheContract`,
    `..._EveryCommandTheExecutableCarriesDeclaresItsContractAsync`,
    `..._BootstrapRefusesToReadWithoutWhatItDeclaresRequiredAsync` and
    `..._RegisterClientRefusesToReadWithoutWhatItDeclaresRequiredAsync`.
  - LIB-TEST-002 AC1 and REF-001 AC2 run in `release.sh`, which no test runs. They were
    verified by hand in a scratch repository: a dropped refusal code, an added argument,
    swapped output lines, a renamed member and an optional argument made required, each
    against the version bump it needs.
- Q19, LIB-API-002 AC2, the configuration half (`a5efe2f7`): the conformance sample enrols
  its first administrator with `SoftwarePasskey`, signs in and sets `policy.default` at
  `PUT /admin/config/{key}`.
  - `LibraryStructureTests.LIB_API_002_AC2_TheConformanceSampleChangesPolicyDefaultAtTheConfigurationEndpoint`;
  - `SampleConfigurationTests.LIB_API_002_AC2_TheSamplesPolicyDefaultIsTheOneItsAdministratorSetAsync`.

**Built under D-207**

- Q20 to Q23, a host job's context (`01b0b0b0`, `72ceaa19`):
  - IDN-PRIN-001 AC6, AUTHZ-GATE-005, AUTHZ-GATE-006 (answers 20, 21): a declared job is
    admitted for its declared permissions on its organization's records through the check,
    the filter, the fragment and the org-wide `RequireAsync`, and no restriction is judged
    for it. A permission not declared, another organization, a subject whose deletion is
    requested and an erased subject are each refused `authz.denied` and recorded with the
    job's principal and reason.
    - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobCaseDecidesTheWayTheTableSaysAsync`
      (six `Jobs` rows, through the check, the expression and the fragment);
    - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobOrganizationCaseDecidesTheWayTheTableSaysThroughTheCheckAsync`
      (six `JobOrganizations` rows, the in-request case among them);
    - `ExplanationTests.IDN_PRIN_001_AC6_ADeclaredJobsRefusalsAreRecordedNamingItsPrincipalAndReasonAsync`.
  - BFF-CAP-002 AC5, AUTHZ-GATE-004, AUTHZ-DERIVE-007 (answers 21, 22), in `JobContextTests`:
    - `BFF_CAP_002_AC5_CapabilitiesGivenAJobsContextAreRefusedInARequestAndOutsideOneAsync`;
    - `IDN_PRIN_001_AC6_TheExplanationGivenAJobsContextIsRefusedAsync`;
    - `AUTHZ_DERIVE_007_AJobIsRefusedTheViewAndIsNotListedByItAsync`.
  - AUTHZ-DERIVE-005 and AUTHZ-GRANT-003 (answer 23): a refresh records the caller's acting
    account as granter, or the nil subject for a system principal, with the reason
    `derivation:<relationship>`, and records each grant it writes or takes back. A system
    principal's refresh is no longer refused; the test that asserted it was replaced. In
    `MaterialisationTests`:
    - `AUTHZ_DERIVE_005_AC7_APersonsRefreshNamesThePersonAndRecordsEachGrantAsync`;
    - `AUTHZ_DERIVE_005_AC7_AJobsRefreshOutsideARequestNamesTheNilGranterAndTheJobAsync`;
    - `AUTHZ_DERIVE_005_ASystemPrincipalsRefreshNamesTheNilGranterAndThePrincipalAsync`;
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
- The observation on the testing package's tests (`b3702a76`): the deployment they run
  logs errors to the console. Verified by a fault thrown in a scratch run, not committed:
  the failed test's output named the exception's type and frames; its message is not
  logged (CONV-LOG-003, BFF-ERR-002 AC2).
- Q27, OPS-SEC-001 AC5, LIB-API-001 AC2, LIB-TEST-002 AC1 (`40a2066f`), in `CommandFaultTests`:
  - `OPS_SEC_001_AC5_AFaultThatCarriesNoCodeEndsEveryCommandWithSystemFaultAsync` (each of
    the seven commands);
  - `OPS_SEC_001_AC5_AFaultClearsEveryKeyTheCommandReadAsync`. That each key-reading
    command clears its keys on a fault was verified by review: each declares
    `using var held = new HeldKeys()`, whose disposal the test proves.
  - `commands.txt` lists `system.fault` for every command, held by
    `CommandContractTests.LIB_API_001_AC2_TheCommandsAreTheContract`.
- Q28, AUTH-OIDC-003 AC4 and CONV-DESIGN-003 AC13 (`67a18bc5`): a reuse ends the spine,
  revokes the grant and every token of it, and writes `auth.oidc.refreshreused` in one
  outermost unit of work.
  - `OidcServiceTests.AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndRecordsItInOneCommitAsync`
    and `..._AFailedCommitLeavesTheGrantStandingAsync`;
  - `OidcFlowTests.AUTH_OIDC_003_AC4_AReuseRevokesTheSpineTheGrantAndEveryTokenOfItAsync`;
  - `TokenReuseStoreTests.AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndItsRecordInOneCommitAsync`
    and `..._WhereTheCommitFailsNoneOfTheFamilyIsRevokedAsync`.
  - A reused token carrying no session or no subject claim keeps the path it had before
    D-207 (the provider revokes the grant and its tokens; no record, no spine), which Q40
    holds.
- Q29, INT-GEN-003 AC3, INT-SMS-005 AC3, BFF-MACH-003 AC2 (`d3222728`):
  `DeliveryReportEndpointTests.INT_GEN_003_AC3_ARejectedReportIsRecordedAgainstItsSourceAndLoggedWithoutItAsync`.
- Q31, AUTH-RECOV-007 AC5 (`a3535a4c`), in `CredentialServiceTests`:
  `AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync` and
  `AUTH_RECOV_007_AC5_ASignInsWriteWhileTheRemovalRunsIsNotOverwrittenAsync` (with fakes;
  the database's part is the `FOR UPDATE` lock the decision reads under).
- Q32, REG-SESS-002 AC4, REG-INV-002 (`8cac8331`), in `RegistrationServiceTests`:
  `REG_SESS_002_AC4_ASignedInBeginCommitsTheAttachmentWithItsRefusalAsync` and
  `CONV_DESIGN_003_AC8_ASignedInBeginCarryingAnInvitationInsideAnOpenUnitOfWorkIsAFaultAsync`.
- Q30, CONV-DESIGN-002, CONV-DESIGN-003 AC13, AUTH-FACT-002b (`3ccfb35c`), for seven of
  the eight operations (`EditProfileAsync` waits on Q36), in `StepUpBeforeTheUnitOfWorkTests`:
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
  AUTHZ-TEST-001 (`4ca48d14`): `IGrantAdmission`, internal to `Janus.Authorization` and
  implemented by `AccessGate`, judges the acting account's restriction, then
  `system:administer` in the administrative organization and `grant:manage` in the
  grant's organization as the single check decides each, and records one refusal naming
  `grant:manage` in the grant's organization only where neither admits. `GrantService`
  asks it at the gate step and again under the lock.
  - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryGrantAdmissionCaseDecidesTheWayTheTableSaysThroughTheAdmissionAsync`
    (nine `GrantAdmissions` rows, each with its refusal count and, for a denial, the
    record's permission and organization);
  - `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync`,
    two new `Operations` rows: a grant written and revoked in another organization by a
    system administrator, and one by a system administrator restricted since the gate
    step;
  - `GrantEndpointTests.AUTHZ_SCOPE_001_AC4_ASystemAdministratorWritesAndRevokesTheFirstGrantOfACreatedOrganizationAsync`.
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
    through `StepUpGuard` and `StepUpGate.RequireAsync`, was traced. One path still
    reaches `AllowsAsync` inside a unit of work (Q41).
- Q37, LIB-TEST-003 AC4 (`20374808`): `InMemorySmsTransport` reports
  99999999999999.9999.
  - `TransportTests.LIB_TEST_003_TheSmsTransportKeepsEveryMessageByDestinationAsync`;
  - `StandInBalanceTests.LIB_TEST_003_AC4_TheBalanceJobRecordsTheTransportsBalanceAndRaisesNoFailureAsync`
    (PostgreSQL, the library's `sms-balance` job run twice, both readings recorded, no
    `background-job-failed`); the first clause stays proven by
    `StandInTests.LIB_TEST_003_AC4_TheStandInsRaiseNoDegradationAsync`.
- Q38, a job's consent step, IDN-PRIN-001 AC6, AUTHZ-CONCEAL-004 AC4 and AC5 (`91b326d6`,
  `013e948c`; no code changed, the library was as the chapters now read). The test
  deployment's job `digest` also declares `document:recommend`.
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
- Q40, AUTH-OIDC-003 AC2 and AC4 (`86e80903`): every reuse takes the one path, by the
  library's statements in one outermost unit of work.
  - `OidcServiceTests.AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync`,
    `..._AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderNoAccountAsync`,
    `..._AC2_AReuseNamingOnlyASessionIsRecordedUnderTheSessionsAccountAsync` and
    `..._AC2_TheTokensAccountIsRecordedBeforeTheGrantsAndTheGrantsBeforeTheSessionsAsync`;
  - `TokenReuseStoreTests.AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync`
    and `..._AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderTheNilSubjectAsync`
    (PostgreSQL, the record's acting identity, `subject` and `details.session`).
  - That the provider's revocation path is gone was verified by reading `TokenReuse`.
- The criteria D-208 brought into line (`cef80da1`, `6c4837a8`, `dc4aff78`; tests only, the
  library was as the chapters now read):
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
- The test servers (`36b09de3`, `24f066ea`, and the sample's in `f52c180a`): the in-memory
  servers of `Janus.Conformance.Tests`, `Janus.Hosting.Tests` and the sample's tests never
  ran a response's completion callbacks, so no request's service scope was disposed and
  each scoped context kept its pooled connection; the sample's deployment ran out of
  PostgreSQL connections after about eight customers. Each server now runs them before the
  request ends. No criterion states a bounded connection count, so the tests are named for
  what they show:
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
    showed (a phone for each person, as `registration.phone` requires by default; the
    workshop's foreign keys; the export's value names under the library's name rule;
    its server's completion callbacks);
  - `e9adae95` test(samples): give the sample's organization its first grants.
  - D-207 answer 33: `samples/Directory.Build.props` imports the root one, restores without
    a lockfile and removes MinVer; the root `.editorconfig` names `samples/HostSample` among
    the source projects and `samples/HostSample.Tests` among the test projects;
    `sample-host.sh` runs `dotnet format --verify-no-changes` over `HostSample.slnx`.
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

## 2. Items in the round not implemented

- **LIB-PKG-001 AC3 and LIB-PKG-003 AC1:** proven in plan section 4, steps 1 and 2.
- **LIB-API-001 AC1:** known issue 45 of the fourth corrections report (what
  "documented" asks of the repository).
- **CONV-DESIGN-003, the outermost begin of `PhoneSignals.AllowsAsync`'s `risk` unit of
  work:** waits on Q41. Until it is answered, a `risk` answer reached inside an open unit
  of work joins it rather than faulting.
- **INF-BG-001, the background worker's wait:** waits on Q42. A wait past
  `Task.Delay`'s largest delay throws and stops the host; seen once per sample run, after
  a fixture whose clock moves 31 days was torn down, failing no test.
- **CONV-DEP-004:** carried into Milestone 2 as a known issue (plan section 4); no code.
- **`11` section 5, `13` R-M20, `15` sections 3.7 and 6.3, and `19` INF-HOST-003:**
  documentation, runbook and infrastructure; no code in this round. INF-HOST-003's
  `migrate` document is proven by OPS-MIG-001 AC4.

## 3. Resolved by rule

1. **`src/Janus.Cli/KeyDocument.cs`, `migrate`'s document.**
   - Out of step: OPS-MIG-001 refuses a document that "carries no connection" and says
     nothing of a connection that is blank or not text.
   - Governing: the `10` section 1 row of `model.startup.secretunavailable`, which covers
     a secret "supplied empty or unusable".
   - Rule: a blank or non-text connection is no connection, so it is refused with
     `details.key` `input`.
2. **`tests/Janus.Storage.Tests/Migrations/MigrationRunTests.cs`.**
   - Out of step: the test ran `dotnet ef database update` as "the migration tool the
     pipeline runs".
   - Governing: OPS-MIG-001 (D-204) makes that step `migrate`.
   - Rule: the case moved to `tests/Janus.Cli.Tests/MigrateTests.cs` and is named for
     OPS-MIG-001 AC2, which it proves. Only the tool it runs changed.
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
   - Governing: the generator, which writes no `?` on a string.
   - Rule: they render as `string`.
10. **`POST /auth/begin`, the 200 answer (`9216f713`).**
    - Out of step: `SignInChallengeView` wrote the member `webAuthn`.
    - Governing: `09` (`POST /auth/begin` and every other sign-in answer) spells it
      `webauthn`, and `09` is authoritative for shapes.
    - Rule: the member is written `webauthn`, with `endpoints.txt` and a changelog line.
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
- **Q16. The held commits `78ae1925` and `bd1b5da0`.**
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
  - Answered by D-207 answer 23; built (section 1). A refresh asked while a request is
    served, which Q39 held, and the limits on a system principal's refresh are built
    under D-208 (section 1).
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
  - Parked: as section 2 states.
  - Answered by D-207 answer 30; built for seven of the eight operations (section 1).
    `EditProfileAsync` and the outermost begins Q30 parked are built under D-208 (Q36),
    save the outermost begin of `AllowsAsync`'s `risk` unit of work, which Q41 holds
    (section 2).
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
  - Parked: every LIB-TEST-004 AC2 test, and so the sample host's gate run (section 2).
  - Answered by D-207 answer 34; built (section 1), the sample host's tests through
    Q35's answer (section 1, "Built under D-208").

Observations, not questions, answered by D-207:
- A host's own `IUnitOfWork` implementation was treated by `UseUnitOfWorkAsync` as not
  begun. It is now refused with a message naming that, with a test (section 1).
- In one run of the testing package's tests, `PUT /register/age` answered 500
  `system.fault` in the staff invitation flow after two failed registrations in the same
  deployment. The testing package's tests now log errors (section 3, record 15).

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
  - Parked: D-207 answer 34 (section 2), and with it the sample host's AC2 tests (Q34's
    fixture).
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
  - Parked: `EditProfileAsync`'s step-up order, and the outermost begins Q30 parked
    (section 2).
  - Answered by D-208 answer 36 (reading A); built (section 1), save the outermost
    begin of `AllowsAsync`'s `risk` unit of work, which Q41 holds (section 2).
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
  - Parked: a job's consent step (section 2).
  - Answered by D-208 answer 38 (reading A); proven (section 1), no code changed.
- **Q39 (Tier 3). What a refresh refused while a request is served records.**
  - AUTHZ-DERIVE-005 AC7 records it as the gate records a refusal of background work. The
    gate's row carries `details.permission` and `details.resourceType`, and its
    resolution builds the AUTHZ-GATE-004 explanation, whose `permission` is required. A
    refresh asks no permission; it names a relationship and a `ResourceId`.
  - Parked: that refusal (section 2).
  - Answered by D-208 answer 39; built (section 1).
- **Q40 (Tier 3). What a reuse writes when the redeemed token carries no session or no
  subject claim (D-207 answer 28).**
  - AUTH-OIDC-003's values and `10` 5.24 give the family as spine, grant and tokens, and
    the record as written in the unit of work that revokes them, and say nothing of a
    token naming no session or subject. `TokenIssue` and `AuthorizationIssue` put the
    session claim on every token, so no endpoint reaches the path.
  - Open: whether the grant and its tokens are revoked there by the library's statements
    in an outermost unit of work or by the provider's managers, and whether an audit
    record is written, naming the grant's subject and no session.
  - Parked: that path, which keeps its behaviour from before D-207 (section 2).
  - Answered by D-208 answer 40; built (section 1).

The refresh's reading (section 1, Q23's build): D-208 confirmed it for a declared job
outside a request and for the drift check, within the limits a system principal has,
which are built (section 1, "Built under D-208").

**Unanswered.**

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
  - Parked: that outermost begin (section 2).
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
  - Parked: that fix (section 2).
- **Q43 (Tier 3). A restricted account's list filter and fragment for a modifying
  permission.**
  - `FilterAsync` and `FragmentAsync` answer a rule matching nothing (`AccessGate.cs`,
    about line 589), where the check refuses `authz.restricted`. AUTHZ-GATE-006's values
    say the gate "refuses every modifying one with `authz.restricted`" and do not say
    how a filter or a fragment answers it. No record is written either way.
  - Readings: **A:** a filter matching nothing is how a list refuses; **B:** the filter
    and the fragment refuse `authz.restricted` as the check does.
  - Parks nothing; the behaviour predates the round.

## 5. Gate result

Fast checks on every commit, and after each merge of a part:
- build with warnings as errors, the analysers and `dotnet format --verify-no-changes`
  over `Janus.slnx`, all green;
- the unit and contract tests, all green on the working branch. In a part's own checkout
  the contract test `ProductNameTests.CONV_NAME_001_AC2` reads the checkout's `.git` file,
  which names the clone's path, and fails there alone.

The pipeline's runs on `07fb7831`, the tip of the last push, were green: the push run
39716104173 and the pull request run 39716105180. Nothing failed, so nothing was fixed.

The full gate ran once on the development machine at `bc39d890`, after the last merge of
a part, job by job as `.github/workflows/gates.yml` runs it, over the merge base with
`main` (`b6d14fe`) to `HEAD`:

| Job | Result |
|---|---|
| Locked restore, build, public surface, format | green |
| Unit tests | 4632 of 4632 |
| Contract tests | 177 of 177 |
| Unicode tables run and diff, tool restore | green |
| Integration tests | 1318 of 1318 |
| Policy coverage | 3 of 3 |
| Truth-table change, truth-table suite | green, 170 of 170 |
| Analyser rules, permitted outcome, forbidden log values | 23 of 23, 4 of 4, 4 of 4 |
| Dependency allow-list, internals allow-list | 2 of 2, 1 of 1 |
| Forbidden markers, acceptance test names, commit message, changelog | green |
| Destructive operations | reported only (the gate is disabled) |
| Dependency vulnerabilities | green, no package with a known vulnerability |
| Double migration | both runs applied through `migrate` |
| The sample host builds from the packages packed from the same commit, and its tests pass | green, `.github/gates/sample-host.sh` after a locked restore: the pack, the sample's restore from the folder feed, the tool install, its build with warnings as errors, `dotnet format --verify-no-changes` over `HostSample.slnx`, and its tests, 12 of 12 |

- The full-history secret scan (OPS-DEP-004) ran after the gate, gitleaks 8.30.1 under
  `.gitleaks.toml` over every commit of every branch and tag (1536 commits): no finding.
  It runs again before the push of this commit.
- The pipeline run, the gate of record, runs on the push of this commit.
