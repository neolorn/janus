# Round H1: the host's way in, its privacy duties, its setup and its tests

Status: every item no question holds is built. Sixteen questions (Q4 to Q19) wait on the
owner, and the items they hold are listed in section 2. The full gate result is in
section 5.

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
  - section 5.28: `SystemOperation.Migration`;
  - sections 5a and 5b: the delivery kinds and changes, through IDN-LIFE-003a;
  - REF-001 AC3 and AC4, at base.
- FE-COMP-006, the server side:
  - criterion 1b: `TheNoticeAnswersTheCookiesItsVersionListsAsync`;
  - criterion 3: `ExportEndpointTests.PRIV_RIGHT_003_AC5_TheReadableFormatCarriesTheContributorsSectionsAfterTheLibrarysAsync`.

## 2. Items in the round not implemented

- **BFF-HOST-001 AC5:** waits on Q13.
- **BFF-CAP-002 AC3, `CapabilityFields`, and BFF-ORDER-001 stage 10:** wait on Q12.
- **LIB-PKG-001 AC3 and LIB-PKG-003 AC1:** proven in plan section 4, steps 1 and 2.
- **LIB-API-001 AC1:** known issue 45 of the fourth corrections report (what
  "documented" asks of the repository).
- **LIB-API-001 AC2, REF-001 AC2 and LIB-TEST-002 AC1:** the commands' contract list
  waits on Q18.
- **LIB-API-002 AC2, the configuration half:** waits on Q19. The sample host half
  waits with LIB-TEST-004.
- **LIB-HOST-001:**
  - the roles row waits on Q16;
  - the jobs row waits on Q15;
  - the host's unit of work waits on Q7.
- **LIB-TEST-003:**
  - AC1, AC3 and AC5: `TestDeployment` waits on Q5, and `TestPeople` and `TestSession`
    depend on it;
  - the package reference to `System.Formats.Cbor`: waits on Q4. `SoftwarePasskey`
    builds against the copy the framework serves.
- **LIB-TEST-004, and its job in CONV-GATE-001:** wait on LIB-TEST-003's `TestDeployment`
  and `TestPeople` (Q5). Q14 is met there too.
- **On `part/authz-host`, held in two local commits (`78ae1925`, `bd1b5da0`) and not
  merged:** wait on Q16:
  - AUTHZ-INHERIT-002 AC5, AUTHZ-INHERIT-003 AC3 and AUTHZ-GRANT-003;
  - AUTHZ-GRANT-004 AC7 and AUTHZ-MODEL-004;
  - `POST /admin/roles`;
  - `10` section 1 `authz.resource.inuse`;
  - sections 5.28 `role-seeding` and `role-declaration`.
- **AUTHZ-INHERIT-002 AC1, the host's row:** waits on Q7.
- **IDN-PRIN-001 AC6 and AC7, OPS-ALERT-006, INT-SMS-003's job part and the job names of
  `10` 5.29:** wait on Q15.
- **PRIV-RIGHT-003 AC5, the machine format:** waits on Q8.
- **PRIV-RIGHT-003, the name faults of an answered section or value and of a
  contributor:** wait on Q9 and Q10.
- **A type with two encrypted fields under one name:** Q11; nothing built.
- **OPS-MIG-001 AC5 (`migrate --report`):** waits on Q6.
- **`10` 5.29's principal `migrate` as a code object:** waits on Q17.
- **CONV-DESIGN-008, `System.Formats.Cbor` in the allow-list:** waits on Q4.
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

**Unanswered.**

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
- **Q5 (Tier 2). `TestDeployment`'s `register-client` arguments.**
  - The command needs `--name`, `--kind`, `--redirect` and `--scopes`. `SignOnClient`
    gives a client identifier only.
  - Parked:
    - `TestDeployment`, with `TestPeople` and `TestSession`;
    - LIB-TEST-003 AC1, AC3 and AC5;
    - LIB-TEST-004;
    - the sample host's row of CONV-GATE-001.
- **Q6 (Tier 2). `migrate --report`.**
  - OPS-MIG-001 states neither its output form nor its exit code where a destructive
    statement is found.
  - Parked: OPS-MIG-001 AC5.
- **Q7 (Tier 2). `UseUnitOfWorkAsync`, "a context that holds a connection of its own".**
  - Readings:
    - **A:** an open connection with its own transaction;
    - **B:** any context configured with a connection or a connection string.
  - The signature is not stated either.
  - Parked:
    - AUTHZ-INHERIT-002 AC1's host row;
    - `UseUnitOfWorkAsync`;
    - the kept-write fault.
- **Q8 (Tier 2). The export's machine format.**
  - Readings:
    - **A:** flat, with built `section.index.value` keys;
    - **B:** "records, each an object of its named values".
  - Keys can collide under A.
  - Parked: PRIV-RIGHT-003 AC5's machine half.
- **Q9 (Tier 2). The name rule on an answered section's or value's name.**
  - `PlaceName` is internal to `Janus.Core`, and `Janus.Privacy` holds no grant to it.
  - Parked: that fault.
- **Q10 (Tier 2). `details.declaration` for an export contributor's name fault.**
  - Readings: **A:** the bare name; **B:** `contributor.<name>`.
  - Parked: the contributor name startup checks.
- **Q11 (Tier 3). Two encrypted fields of one type under one name.**
  - Their values can be swapped undetected, and no refusal is stated.
  - Nothing built.
- **Q12 (Tier 2). `CapabilityFields`.**
  - The `can` and `requires` keys could be the action or the permission identifier.
  - The return type is not stated.
  - Parked:
    - `CapabilityFields`;
    - BFF-CAP-002 AC3;
    - BFF-ORDER-001 stage 10's check.
- **Q13 (Tier 3). BFF-HOST-001 AC5.**
  - It asks for a new session identifier, against the `SessionId` kept at rotation.
  - Parked: AC5.
- **Q14 (Tier 2). `AddJanus` clears the HTTP JSON `TypeInfoResolverChain`.**
  - A host endpoint's own types then fail to serialize.
  - Parks nothing built. The sample host meets it.
- **Q15 (Tier 2). A job principal refused "while a request is being served".**
  - No mechanism tells a job's run from a request.
  - Parked:
    - `Job` with its refusals and its admission at the gate;
    - IDN-PRIN-001 AC6 and AC7;
    - OPS-ALERT-006;
    - INT-SMS-003's job part.
- **Q16. The held commits `78ae1925` and `bd1b5da0`.**
  - `78ae1925` carries a 73-character body line that the commit-message gate
    (CONV-VCS-003) fails, and no commit may be rewritten.
  - It also writes `authz.grant.revoked` with subject none, which is reading (b) of known
    issue 1, still parked.
  - Parked: the items of section 2 they hold.
- **Q17 (Tier 2). `10` 5.29's command principal `migrate`.**
  - Section 5.29 makes a principal the name written on the audit rows a command writes.
    `migrate` writes none, calls no gate and raises no job failure.
  - Readings, with the smallest fix for each:
    - **A:** a row of 5.29 only, with no code object;
    - **B:** `migrate` writes an audit row naming it (a new action and row);
    - **C:** an unused `SystemPrincipal` constant held by `LibraryMigrations`.
  - Parked: the principal as a code object.
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

## 5. Gate result

Fast checks on every commit:
- build with warnings as errors, the analysers and `dotnet format --verify-no-changes`
  over `Janus.slnx`, all green;
- the unit tests, all green;
- the contract tests, which carry the one failure below.

The full gate ran once on the development machine, at `da6e1a3`, job by job as
`.github/workflows/gates.yml` runs it, over the merge base with `main` (`b6d14fe`) to
`HEAD`:

| Job | Result |
|---|---|
| Locked restore, build, public surface, format | green |
| Unit tests | 4429 of 4429 |
| Contract tests | 167 of 168; `ErrorCodesTests.REF_001_AC1_EveryLiveRowOfTheReferenceIsACodeInTheSource` fails on `authz.resource.inuse`, whose code is in held commit `78ae1925` (Q16) |
| Unicode tables run and diff, tool restore | green |
| Integration tests | 1183 of 1183 |
| Policy coverage | 3 of 3 |
| Truth-table change, truth-table suite | green, 121 of 121 |
| Analyser rules, permitted outcome, forbidden log values | 23 of 23, 4 of 4, 4 of 4 |
| Dependency allow-list, internals allow-list | green |
| Forbidden markers, acceptance test names, commit message, changelog | green |
| Destructive operations | reported only (the gate is disabled); this round adds the `ck_outbox_kind` drops of `OweNothingAfterAnExport` and `DeliverPurposeChanges` |
| Dependency vulnerabilities | green |
| Double migration | both runs applied through `migrate` |

- The sample host's job does not exist yet: LIB-TEST-004 waits on Q5.
- The full-history secret scan (OPS-DEP-004) ran after the gate. It ran gitleaks 8.30.1
  under `.gitleaks.toml` over every commit of every branch and tag, and found nothing.
- The pipeline run, the gate of record, waits on the branch being pushed.
