# Round H1: the host's way in, its privacy duties, its setup and its tests

Status: every item no question holds is built. D-206 answered Q4 to Q19, and the items
they held are built (section 1, "Built under D-206"), save the sample host, which Q33
and Q34 now hold. Fifteen questions (Q20 to Q34) wait on the owner, and the items they
hold are listed in section 2. The full gate result is in section 5.

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

## 2. Items in the round not implemented

- **LIB-PKG-001 AC3 and LIB-PKG-003 AC1:** proven in plan section 4, steps 1 and 2.
- **LIB-API-001 AC1:** known issue 45 of the fourth corrections report (what
  "documented" asks of the repository).
- **IDN-PRIN-001 AC6, a host job's admission:** waits on Q20 (whose restriction applies to
  a job), with OPS-ALERT-006's admitted job and the truth-table row "admitted outside a
  request". The gate refuses a job's context in a request and, outside one, refuses it on
  any check that asks the org-wide question (Q21).
- **The org-wide `RequireAsync` and `CapabilitiesAsync`, `ExplainAsync` and
  `WhoCanAccessAsync`, and `IDerivationMaterialiser`, for a job's context:** wait on Q21,
  Q22 and Q23.
- **LIB-TEST-004, the sample host, and its job in CONV-GATE-001:** held on the local
  branch `part/sample`, unmerged and unpushed (the working guide's section 6, D-206),
  since the sample's tests cannot pass until Q33 and Q34 are answered:
  - `7dd72348` feat(samples): add the kite workshop sample host;
  - `1a88bb56` test(core): check that the sample host references no library project;
  - `a1f04bfe` ci(gates): add the sample host's gate script;
  - `dae661a5` ci(gates): run the sample host in the pipeline.
  - The sample's build files (`samples/Directory.Build.props`, `samples/.editorconfig`)
    wait on Q33 and are not committed.
  - AC1: `.github/gates/sample-host.sh` and the `sample-host` job; no test decides it.
  - AC2, in `samples/HostSample.Tests`, each failing in its fixture's setup on Q34 (the
    invitation into the sample's organization answers 403 `authz.denied`):
    - `WorkshopEndpointTests.LIB_TEST_004_AC2_ARecordIsWrittenAndReadWithItsPersonalFieldEncryptedAsync`,
      `..._AnEndpointIsRefusedWithoutASessionAndOneMarkedAsAnsweringAnyoneIsReachedAsync`,
      `..._ABoundActionIsRefusedWithoutItsStepUpAsync`,
      `..._ARecordRegisteredAndRolledBackLeavesNeitherAsync`,
      `..._ARegistrationIsRemovedAsync`, `..._AnExportCarriesTheHostsSectionAsync`,
      `..._AWithdrawalIsHandledByTheHandlerAsync` and
      `..._AnObjectionIsHandledByTheHandlerAsync`;
    - `WorkshopEndpointTests.LIB_TEST_004_AWorkshopsListIsFilteredByTheGateAndCarriesCapabilitiesAsync`;
    - `ErasureTests.LIB_TEST_004_AC2_AnErasedCustomersHostFieldNoLongerDecryptsAsync`.
  - AC2, "a job admitted for its declared permission and refused for another": waits on
    Q20. The sample declares the job `follow-up-calls`; no test of its admission is
    written.
  - AC3: `LibraryStructureTests.LIB_TEST_004_AC3_TheSampleHostReferencesNoProjectOfTheLibrarysSolution`,
    passing on the branch.
- **CONV-DESIGN-003, the outermost begin of the operations the step-up guard reaches:**
  waits on Q30. Parked: the record `PhoneSignals.AllowsAsync` writes on a `Risk` answer,
  and the operations whose kept write is that record or that reach it through the step-up
  guard (the `Risk` path of the second step's ask and of the text sign-in link's ask,
  `CredentialService.BeginTextCodeAsync`, `RecoveryService.BeginAsync`, and the
  consideration of every operation bound to a step-up gate).
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
  - Answered by D-206 answer 5; built (section 1), save LIB-TEST-004, which Q33 and Q34
    now hold (section 2).
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

**Unanswered.**

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
- **Q21 (Tier 3). The org-wide `RequireAsync` and `CapabilitiesAsync` for a job's
  context.**
  - Neither the item nor the gate says how a job's context answers the org-wide question.
  - Parked: both; the org-wide check refuses a job's context.
- **Q22 (Tier 3). `ExplainAsync` and `WhoCanAccessAsync` for a job's context.**
  - Parked: both for a job's context.
- **Q23 (Tier 3). `IDerivationMaterialiser` with a job's context.**
  - The granter, the reason and the principal of what it writes are not stated.
  - Parked: the materialiser for a job's context.
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
- **Q25 (Tier 3). A joined context returned to a `DbContext` pool before its unit of work
  ends.**
  - A host that ends its scope without committing or rolling back can return the joined
    context to the pool while the unit of work is open, and the pool's reset keeps the
    connection, so another lease could run on it inside the transaction.
  - Parks nothing.
- **Q26 (Tier 2). The public signatures of `TestDeployment`, `TestPeople` and
  `TestSession`.**
  - LIB-TEST-003 states what they do and not their signatures; those built are in
    `src/Janus.Testing/PublicAPI.Unshipped.txt`. The customer's date of birth (1990-01-01)
    and the passkey's label ("Software passkey") are not stated either.
  - Parks nothing.
- **Q27 (Tier 2). A fault in a `Janus.Cli` command that is not a refusal.**
  - OPS-SEC-001 states the refusal line and exit 1, and `migrate` writes `system.fault`
    for a database error; any other exception escapes `Program.RanAsync` with no line.
  - Readings, with the smallest fix for each:
    - **A:** every command writes `{"code":"system.fault"}` and exits 1 (one catch, one
      test);
    - **B:** a fault is not a refusal and may end the process (a sentence in `06`).
  - Parks nothing.
- **Q28 (Tier 3). A refresh token's reuse commits its kept writes in three
  transactions.**
  - `TokenReuse` revokes the grant's tokens and the grant, each committing alone, before
    `OidcService.ReuseAsync` ends the session family and writes the audit record in a unit
    of work of its own; CONV-DESIGN-003 commits a refusal's kept writes together.
  - Parks nothing; `TokenReuse` is unchanged.
- **Q29 (Tier 2). "A rejected callback's count and record" (INT-SMS-005).**
  - The callback guards and the provider-event intake write the rejection as the
    `CallbackLog.Refused` line; `DeliveryReports.ReportAsync` writes no record of it.
  - Readings, with the smallest fix for each:
    - **A:** the record is the log line, and the delivery report's route lacks it (log it
      there);
    - **B:** the record is a stored row (name it in `10` and write it on every route).
  - Parks nothing.
- **Q30 (Tier 3). The phone signal asked and its consideration recorded inside another
  operation's unit of work.**
  - `PhoneSignals.AllowsAsync` is to be asked before an operation's unit of work begins
    (CONV-DESIGN-002, D-186). The step-up guard calls it inside the caller's level: for
    example `AccountService.EditProfileAsync` begins (`AccountService.cs:206`), then
    `StepUpGuard.WithheldAsync` calls `signals.AllowsAsync` (`StepUpGuard.cs:447`), whose
    `BeginAsync` joins. A step-up refusal then rolls the consideration back. About 25
    callers of `StepUpGuard.PassedAsync` take this path.
  - Parked: as section 2 states.
- **Q31 (Tier 2). A write kept on a refusal that CONV-DESIGN-003's list does not name:
  `CredentialService.RemoveAsync`.**
  - `LossReports.SuspendAsync` commits the suspension, the report and its audit record,
    then the operation refuses `credential.last_second_factor` (AUTH-STEP-006).
  - Readings: **A:** add it to the list, so the operation begins the outermost unit of
    work; **B:** the suspension is the outcome, not a refusal's kept write (a sentence in
    CONV-DESIGN-003).
  - Parks nothing; unchanged.
- **Q32 (Tier 2). The same, at `RegistrationService.BeginAsync` signed in.**
  - `InvitationOpening.OpenAsync` commits the invitation's attachment, then the operation
    refuses `registration.signed_in` (REG-SESS-002).
  - Readings as Q31. Parks nothing; unchanged.
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
- **Q34 (Tier 3). No public path gives anyone a grant in an organization that
  `POST /admin/organizations` creates.**
  - AUTHZ-SCOPE-001 evaluates invitations and grants in the organization they belong to,
    so an invitation with roles needs `membership:manage` and `grant:manage` there, and
    `POST /admin/grants` needs `grant:manage` there. `POST /admin/organizations` is not
    gated and seeds no grant or membership. A grant in the administrative organization
    confers only on its members.
  - Parked: every LIB-TEST-004 AC2 test, and so the sample host's gate run (section 2).

Observations, not questions:
- A host's own `IUnitOfWork` implementation is treated by `UseUnitOfWorkAsync` as not
  begun, and the call throws; no test asserts it.
- In one run of the testing package's tests, `PUT /register/age` answered 500
  `system.fault` in the staff invitation flow after two failed registrations in the same
  deployment. It did not recur in three later runs, and logging was off.

## 5. Gate result

Fast checks on every commit, and after each merge of a part:
- build with warnings as errors, the analysers and `dotnet format --verify-no-changes`
  over `Janus.slnx`, all green;
- the unit and contract tests, all green on the working branch. In a part's own checkout
  the contract test `ProductNameTests.CONV_NAME_001_AC2` reads the checkout's `.git` file,
  which names the clone's path, and fails there alone.

The full gate ran once on the development machine, at `f33bc353`, job by job as
`.github/workflows/gates.yml` runs it, over the merge base with `main` (`b6d14fe`) to
`HEAD`:

| Job | Result |
|---|---|
| Locked restore, build, public surface, format | green |
| Unit tests | 4588 of 4588 |
| Contract tests | 175 of 175 |
| Unicode tables run and diff, tool restore | green |
| Integration tests | 1242 of 1242 |
| Policy coverage | 3 of 3 |
| Truth-table change, truth-table suite | green, 135 of 135 |
| Analyser rules, permitted outcome, forbidden log values | 23 of 23, 4 of 4, 4 of 4 |
| Dependency allow-list, internals allow-list | 2 of 2, 1 of 1 |
| Forbidden markers, acceptance test names, commit message, changelog | green |
| Destructive operations | reported only (the gate is disabled) |
| Dependency vulnerabilities | the job exited 1 with no output, the package listing having failed before it printed; run again by itself, green, no package with a known vulnerability |
| Double migration | both runs applied through `migrate` |
| The sample host builds from the packages packed from the same commit, and its tests pass | red, run from `part/sample` (`dae661a5`) with the job's own steps: the sample's restore fails with NU1010 on MinVer, which waits on Q33. With the build files Q33 parks in place, the part's own run built the sample and ran its 10 tests, all failing in their fixture's setup on Q34 |

- `4d4b7e67` (the allow-list entry of section 3, record 14) followed the gate. Its fast
  checks: locked restore, build, format and the pending-model check green, unit tests
  4588 of 4588, contract tests 175 of 175; the commit-message gate green.
- The full-history secret scan (OPS-DEP-004) ran after the gate. It ran gitleaks 8.30.1
  under `.gitleaks.toml` over every commit of every branch and tag. At `f33bc353` it found
  two matches, the item identifier of record 14, and with `4d4b7e67` it found nothing.
- The pipeline run, the gate of record, waits on the branch being pushed. The round's
  pull request cannot merge until `part/sample` is merged (section 2).
