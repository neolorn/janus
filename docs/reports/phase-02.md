# Phase 2: Authorization

Status: complete, full gate green, no open question. The permission model, the grants,
the roles, the groups, the ancestry closure, the two renderings of one permission rule,
the gate and its explanation, the restriction at the gate, the capability residuals, the
derivations on every path, the refresh of a materialised derivation and the two startup
checks that read the database are built. The three questions this report first carried
are answered by D-161 and applied here; the nine decisions taken in the owner's absence
are in section 4 and, in the same words, in `docs/reports/decisions-pending-review.md`.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTHZ-PRIN-001 | AC1, AC2 | `TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync`, `PermissionRuleTests.AUTHZ_PRIN_001_AC2_NoPredicateIsReachableByOnlyOneOfThePaths`, `GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync`, `GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckNoDerivationReachesNeedsNoRowsAsync` |
| AUTHZ-PRIN-002 | AC1, AC2 | `TruthTableTests.AUTHZ_PRIN_002_AC1_TheExpressionTranslatesToOneCorrelatedQueryAsync`, `PermissionRuleTests.AUTHZ_PRIN_002_AC2_NoRenderingSelectsTheRecordsAPrincipalMayReach` |
| AUTHZ-PRIN-003 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_PRIN_003_AC1_AnUndeclaredResourceTypeRaisesAsync`, `GateBehaviourTests.AUTHZ_PRIN_003_AC2_APrincipalThatResolvesToNoAccountIsRefusedAsync`, `SubjectSetsTests.AUTHZ_PRIN_003_AC2_APrincipalWithNoAccountResolvesToNothingAsync`, `AccessSeamTests.AUTHZ_PRIN_003_AC3_NoPathTurnsAFailureIntoAnAllow` |
| AUTHZ-GRANT-001 | AC2, AC3, AC4 | `GrantTests.AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganization`, `GrantStoreTests.AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganizationAsync`, `ExplanationTests.AUTHZ_GRANT_001_AC2_AGrantOnARecordConfersNothingOverTheOrganizationAsync`, `PermissionRuleTests.AUTHZ_GRANT_001_AC2_TheOrganizationRenderingReadsNoAncestry`, `GrantTests.AUTHZ_GRANT_001_AC3_OneShapeCarriesAccountAndGroupGrants`, `GrantStoreTests.AUTHZ_GRANT_001_AC3_TheSameTableServesAccountAndGroupGrantsAsync`, `GateBehaviourTests.AUTHZ_GRANT_001_AC4_AStoredAndADerivedGrantAnswerAlikeAsync` |
| AUTHZ-GRANT-002 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_GRANT_002_AC1_ADenyOnTheRecordDefeatsAnInheritedAllowAsync`, `GateBehaviourTests.AUTHZ_GRANT_002_AC2_ADenyDefeatsAGrantOnTheWholeOrganizationAsync`, `GateBehaviourTests.AUTHZ_GRANT_002_AC3_RemovingTheDenyRestoresTheAllowAsync` |
| AUTHZ-GRANT-003 | AC1, AC2, AC3 | `GrantStoreTests.AUTHZ_GRANT_003_AC1_AnExpiredGrantIsNotReadWithoutASweepAsync`, `GrantTests.AUTHZ_GRANT_003_AC2_CreationRecordsWhoWhenAndWhy`, `GrantTests.AUTHZ_GRANT_003_AC2_RevocationRecordsWhoWhenAndWhy`, `GrantStoreTests.AUTHZ_GRANT_003_AC2_TheAuditFieldsAreOnTheRowThroughRevocationAsync`, `GrantStoreTests.AUTHZ_GRANT_003_AC3_WhoGrantedThisAndWhenIsAnsweredByQueryAsync` |
| AUTHZ-GRANT-004 | AC1, AC2 | `RoleStoreTests.AUTHZ_GRANT_004_AC1_ARoleAndItsPermissionsAreWrittenAtRuntimeAsync`, `GateBehaviourTests.AUTHZ_GRANT_004_AC2_GrantingTakesEffectOnTheNextRequestAsync` |
| AUTHZ-GROUP-001 | AC1, AC2 | `GroupClosureStoreTests.AUTHZ_GROUP_001_AC1_AUserInATeamInsideADepartmentBelongsToBothAsync`, `GroupClosureStoreTests.AUTHZ_GROUP_001_AC2_NestingDepthIsNotFixedBySchemaAsync`, `SubjectSetsTests.AUTHZ_GROUP_001_AC2_NestingIsFollowedToWhateverDepthIsWrittenAsync` |
| AUTHZ-GROUP-002 | AC1, AC2 | `SubjectSetsTests.AUTHZ_GROUP_002_AC1_TenChecksInOneOperationResolveMembershipOnceAsync`, `SubjectSetsTests.AUTHZ_GROUP_002_AC2_TheResolvedSetIsHeldAndInvalidatedByTheCounterAsync` |
| AUTHZ-INHERIT-001 | AC1, AC2 | `GateBehaviourTests.AUTHZ_INHERIT_001_AC1_AGrantThreeLevelsAboveConfersTheSameAccessAsync`, `GrantStoreTests.AUTHZ_INHERIT_001_AC1_AGrantOnAContainerIsReadForItsContentsAsync`, `GateBehaviourTests.AUTHZ_INHERIT_001_AC2_RemovingTheGrantRemovesTheInheritedAccessAsync` |
| AUTHZ-INHERIT-002 | AC1, AC2, AC3, AC4 | `AncestryStoreTests.AUTHZ_INHERIT_002_AC1_CreatingARecordWritesItsAncestryAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC1_ARollbackLeavesNeitherTheRecordNorItsAncestryAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC2_MovingOutOfEveryContainerClearsWhatWasAboveAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC3_ABulkImportProducesCorrectAncestryAsync`, `PermissionRuleTests.AUTHZ_INHERIT_002_AC4_NoRenderingWalksTheTreeAtRequestTime` |
| AUTHZ-INHERIT-003 | AC1, AC2, AC3 | `AncestryStoreTests.AUTHZ_INHERIT_003_AC1_MovingASubtreeBeneathItsFormerSiblingAsync`, `AncestryStoreTests.AUTHZ_INHERIT_003_AC2_ConcurrentMovesOfOverlappingSubtreesAsync`, `AncestryStoreTests.AUTHZ_INHERIT_003_AC3_NoAncestryRowIsOrphanedAsync` |
| AUTHZ-DERIVE-001 | AC1, AC2, AC3 | `DerivationTests.AUTHZ_DERIVE_001_AC1_ADerivationIsDeclaredWhereContainmentIs`, `GateBehaviourTests.AUTHZ_DERIVE_001_AC2_TheHostsOwnDataDecidesTheNextRequestAsync`, `GateBehaviourTests.AUTHZ_DERIVE_001_AC3_RemovingTheRelationshipRemovesTheAccessAsync` |
| AUTHZ-DERIVE-002 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_DERIVE_002_AC1_ADenyDefeatsADerivedGrantAsync`, `GateBehaviourTests.AUTHZ_DERIVE_002_AC2_ADerivedGrantOnAContainerReachesItsContentsAsync`, `GateBehaviourTests.AUTHZ_DERIVE_002_AC2_AFactOnAnotherContainerReachesNothingHereAsync`, `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync` |
| AUTHZ-DERIVE-003 | AC1 | `DerivationTests.AUTHZ_DERIVE_003_AC1_EachDerivationNamesADeclaredRelationship` |
| AUTHZ-DERIVE-004 | AC1, AC2 | `ModelValidationTests.AUTHZ_DERIVE_004_AC1_ADerivationNamingAnUnindexedColumnIsRefusedAsync`, `VolumeTests.AUTHZ_DERIVE_004_AC2_TheDerivedRuleUsesAnIndex` |
| AUTHZ-DERIVE-005 | AC1, AC2, AC3 | `DerivationTests.AUTHZ_DERIVE_005_AC1_MaterialisationIsDeclaredPerDerivation`, `DerivationTests.AUTHZ_DERIVE_005_AC2_WhereAGrantCameFromIsCarriedOnItAndOnItsExplanation`, `GrantStoreTests.AUTHZ_DERIVE_005_AC2_AMaterialisedGrantReadsBackAsOneAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC1_TheRowsTheRefreshWroteConferTheRoleAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC1_ARefreshOfANonMaterialisedDerivationIsRefusedAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync`, `MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshFindsTheDriftAndCorrectsItAsync` |
| AUTHZ-SCOPE-001 | AC2 | `GateBehaviourTests.AUTHZ_SCOPE_001_AC2_AnAccountInTwoOrganizationsReachesOnlyWhatEachGrantsAsync` |
| AUTHZ-MODEL-001 | AC1, AC2, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_001_AC1_NoLibrarySourceNamesAHostDomainType`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC2_TwoDomainsSharingNothingUseTheSameBinary`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC3_AFieldIsNamedByACompilerCheckedReference` |
| AUTHZ-MODEL-002 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_002_AC1_AddingAResourceTypeNeedsNoLibraryChange` |
| AUTHZ-MODEL-003 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_003_AC1_ATypeDeclaredWithoutAPurposeFailsStartup` |
| AUTHZ-MODEL-004 | AC1, AC2, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_004_AC1_EachRefusedDeclarationCarriesItsOwnCode` over `model.containment.cycle`, `model.type.undeclaredreference`, `model.type.noorganizationpath`, `model.purpose.missingassessment` and `model.derivation.undeclaredreference`; `ModelValidationTests.AUTHZ_MODEL_004_AC1_ARoleAllowingAnUndeclaredPermissionIsRefusedAsync`, `ModelValidationTests.AUTHZ_DERIVE_004_AC1_ADerivationNamingAnUnindexedColumnIsRefusedAsync`, `StartupValidationTests.AUTHZ_MODEL_004_AC1_AStoredRoleAllowingAnUndeclaredPermissionIsRefusedAsync`, `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered`, `AuthorizationModelTests.AUTHZ_MODEL_004_AC3_AnEntityWithoutAPolicyIsFoundByEnumeration` |
| AUTHZ-MODEL-005 | AC1, AC2 | `SerializedModelTests.AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes`, `SerializedModelTests.AUTHZ_MODEL_005_AC2_APermissionModelChangeProducesADiff` |
| AUTHZ-MODEL-006 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_006_AC1_NoRuntimeApiModifiesTheModel` |
| AUTHZ-GATE-001 | AC1, AC2, AC3 | `AccessSeamTests.AUTHZ_GATE_001_AC1_NoPublicSurfaceHandsOutASetOfRows`, `AccessSeamTests.AUTHZ_GATE_001_AC2_EveryOperationNamesWhoIsAsking`, `AuthorizationModelTests.AUTHZ_GATE_001_AC3_EveryQueryableEntityIsEnumeratedAgainstItsPolicy` |
| AUTHZ-GATE-002 | AC1, AC2, AC3 | `PermissionRuleTests.AUTHZ_GATE_002_AC1_EveryRenderingCarriesTheSameValues`, `PermissionRuleTests.AUTHZ_GATE_002_AC1_TheDerivedRenderingNamesWhatTheHostDeclared`, `TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, `TruthTableTests.AUTHZ_GATE_002_AC3_TheFragmentInterpolatesNoValueAsync`, `TruthTableTests.AUTHZ_GATE_002_AC3_AnAliasThatIsNotAnIdentifierIsRefusedAsync` |
| AUTHZ-GATE-003 | AC2 | `DialectContractTests.AUTHZ_GATE_003_AC2_NoClaimOfDialectPortabilityIsDocumented` |
| AUTHZ-GATE-004 | AC1, AC2, AC3, AC4 | `ExplanationTests.AUTHZ_GATE_004_AC1_ADenialNamesThePermissionAndStatesNoGrantMatchedAsync`, `ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAndWhatItWasInheritedFromAsync`, `ExplanationTests.AUTHZ_GATE_004_AC3_OnlyATypeThatDisclosesExplainsToTheCallerAsync`, `ExplanationTests.AUTHZ_GATE_004_AC4_ACorrelationIdentifierResolvesOnlyForASupportRoleAsync` |
| AUTHZ-GATE-005 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`, `PermissionRuleTests.AUTHZ_GATE_005_AC1_APageIsOneStatementNamingEveryRecordByParameter`, `GateBehaviourTests.AUTHZ_GATE_005_AC2_ACapabilityRequiringNothingFurtherSucceedsAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC2_ADerivedGrantReachesTheCapabilityPageAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC3_ACapabilityCarriesWhatTheActionStillRequiresAsync` |
| AUTHZ-GATE-006 | AC1, AC2 | `GateBehaviourTests.AUTHZ_GATE_006_AC1_ARestrictedAccountReadsAndDoesNotModifyAsync`, `AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace` |
| AUTHZ-CONCEAL-001 | AC1, AC2 | `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC1_ATypeDeclaringNothingConceals`, `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC2_DisclosingIsDeclaredAndNotInferred` |
| AUTHZ-CONCEAL-002 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync` |
| AUTHZ-CONCEAL-003 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_003_AC1_TwoRecordsOfOneTypeProduceTheSameRefusalAsync` |
| AUTHZ-CONCEAL-004 | AC1, AC2 | `ExplanationTests.AUTHZ_CONCEAL_004_AC1_TheIdentifierResolvesToTheEntryItWroteAsync`, `ExplanationTests.AUTHZ_CONCEAL_004_AC2_TheIdentifierSaysNothingAboutTheRecordAsync` |
| AUTHZ-CONCEAL-005 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_005_AC1_APermissionTiedToNoRecordIsRefusedAsForbiddenAsync` |
| AUTHZ-CACHE-001 | AC1 to AC8 | `GateBehaviourTests.AUTHZ_CACHE_001_AC1_RevokingTakesEffectOnTheNextRequestAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC1_RevokingAGrantRaisesTheHoldersCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC2_AFailedGrantWriteLeavesTheCounterUnchangedAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC2_ARolledBackMembershipChangeLeavesTheCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC3_AGroupsGrantRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_AMembershipChangeRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_LeavingAGroupRaisesTheLeaversCounterAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC3_TheHeldSetCarriesTheCounterItWasReadAtAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC4_NothingIsKeyedOnASubjectActionAndRecord`, `GateBehaviourTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsTakesEffectAtOnceAsync`, `RoleStoreTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsRaisesNoCounterAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC6_MovingARecordTakesEffectAtOnceAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC7_AnExpiredGrantConfersNothingWithoutASweepAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC8_RestrictingAnAccountTakesEffectImmediatelyAsync` |
| AUTHZ-IMP-001 | AC1, AC2, AC3 | `AccessSeamTests.AUTHZ_IMP_001_AC1_BothFieldsArePresentOnEveryAccessContext`, `ExplanationTests.AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync`, `AccessSeamTests.AUTHZ_IMP_001_AC3_NoFeatureReadsTheTwoIdentitiesAsDiffering` |
| AUTHZ-TEST-001 | AC1, AC2, AC3 | `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`, `TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync` |
| AUTHZ-TEST-002 | AC1, AC2 | `VolumeTests.AUTHZ_TEST_002_AC1_ThePlanIsCapturedAtTheStatedVolumesAsync`, `VolumeTests.AUTHZ_TEST_002_AC2_ThePermissionPredicateUsesAnIndex` |
| AUTHZ-SEAM-001 | AC1, AC2 | `AccessSeamTests.AUTHZ_SEAM_001_AC1_OneTypeStandsBehindTheInterface`, `AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery` |
| CONV-NAME-002 | AC1, AC2 | `AuthorizationModelTests.CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation`, `PermissionsTests.CONV_NAME_002_AC2_NoLibraryOwnedPermissionCarriesAScopeQualifier` |
| LIB-API-001 | AC2 for `10` sections 5.5, 5.6 and 5.11 | `VocabularyContractTests.LIB_API_001_AC2_TheAuthorizationVocabulariesAreTheContract` |
| LIB-API-004 | AC1 | `DialectContractTests.LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific` |
| LIB-HOST-002 | AC1, AC2 | `PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns`, `PermissionRuleTests.LIB_HOST_002_AC1_OnlyWhatTheHostRunsNamesTheRelationItDeclared`, `TruthTableTests.LIB_HOST_002_AC2_TheFilterComposesWithoutMaterialisingRowsAsync` |
| LIB-HOST-004 | AC1, AC2 | `AccessSeamTests.LIB_HOST_004_AC1_AuthorizationAloneCompilesAndRuns`, `GateBehaviourTests.LIB_HOST_004_AC2_ABoundActionIsDeniedWithNoAssuranceProviderAsync` |
| LIB-SEAM-001 | AC1 | `AccessSeamTests.LIB_SEAM_001_AC1_ReplacingWhatEvaluatesIsOneChange` |
| LIB-SEAM-002 | AC2 | `AccessSeamTests.LIB_SEAM_002_AC2_NoFeatureReadsActingAndEffectiveAsDiffering` |

The truth table of AUTHZ-TEST-001 is twenty cases over one shared table, the three
derived cases AC1 names among them, run through the single check, the LINQ expression
and the SQL fragment by
`TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`,
`TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync` and
`TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, and run
a second time against the same deployment with the derivation materialised by
`TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync`.

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| AUTHZ-GRANT-001 | AC1 | Inspection: every permission in `10` section 2.1 and every permission a host declares is a `resource:action` pair, and `Grant` carries subject, role, resource and organization, so each is the item's sentence. A test decides that no second shape exists (`AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery`), not that none could be written. |
| AUTHZ-GATE-003 | AC1 | Inspection: `07` LIB-API-004 states the constraint in the library contract document. The API documentation carrying the same constraint is decided by `DialectContractTests.LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific`. |
| AUTHZ-MODEL-004 | AC2 | By construction and by test: the checks that need only the declaration run inside `AddJanus`, so a declaration that does not validate stops registration before a container exists to serve a request; the two that read the database run in a hosted service inserted at the head of the collection, which `StartupValidationTests.AUTHZ_MODEL_004_AC2_TheChecksStartBeforeEverythingElseRegistered` decides, and `StartupValidationTests.AUTHZ_MODEL_004_AC1_AStoredRoleAllowingAnUndeclaredPermissionIsRefusedAsync` shows the process failing as it starts. |
| AUTHZ-CONCEAL-002 | AC2 | By construction, as the criterion itself directs (D-153): one path answers both refusals and the bytes are identical, which `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync` asserts. |
| AUTHZ-DERIVE-003 | AC2 | Review step: a derivation whose relationship exists only to grant permission is caught when the declaration is read. |
| AUTHZ-DERIVE-006 | AC1 | Nothing in this repository is materialised, so no materialisation decision exists to reference a measurement. The criterion binds the decision, which the owner records in the log. |
| AUTHZ-GATE-002 | AC1 | `PermissionRuleTests.AUTHZ_GATE_002_AC1_EveryRenderingCarriesTheSameValues` compares the three renderings of a rule without derivations. Where a derivation is declared the fragment carries the relation's own parameter, which the three renderings the library runs do not; that rendering is decided by `PermissionRuleTests.AUTHZ_GATE_002_AC1_TheDerivedRenderingNamesWhatTheHostDeclared`. |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTHZ-DERIVE-007, the reverse-lookup view | D-161 decision 3: the view is the phase 8 one and nothing of it is built now. The item's own criteria, which are the cache criteria, are tested here | Phase 8 |
| The drift-check sweep of AUTHZ-DERIVE-005 | The refresh reports what it changed and corrects it in the same run, and `derivation.materialised.driftcheck` exists; running it on a schedule and raising the degradation condition is a background job carrying the host's own sources | Phase 9, OPS-OBS; section 4, decision 8 |
| AUTHZ-MODEL-004, the `Janus.Cli` half | The application carries no command for the validation to run before; the type it would run is the one registered here | Phase 9, OPS-BOOT-001 |
| AUTHZ-GATE-005 AC4 | Frontend code | `18`, milestone 2 step 10 |
| AUTHZ-SCOPE-001 AC1 | There is no session | Phase 3 |
| AUTHZ-CACHE-002 AC1 | There is no session cookie and no token | Phase 3 |
| AUTHZ-MODEL-003 AC2 | Sensitivity drives consent behaviour | Phase 7, `04` PRIV-SENS-002 |
| LIB-SEAM-002 AC1 | The claim is about the whole configuration surface, which the last phase of the milestone completes | Phase 10 |
| `10` section 3, the three administrative roles | Seeded at bootstrap, through the role store built here | Phase 9, OPS-BOOT-001 |

A session that meets a step-up gate is phase 3 (AUTH-STEP-002, AUTH-STEP-006). Until it
exists, an action bound to a gate is refused with `auth.stepup.required` where an
assurance provider is registered and `auth.stepup.unavailable` where none is.

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs`, the `ModelBuilder` list | `src/Janus.Core/DeclaredMember.cs` reads the member a host names in a declaration lambda and so uses `System.Reflection`, which the list did not admit | CONV-CODE-004 AC2 | The criterion leaves reflection to the model builder, and the declared-member reader is part of the model builder, so its file joins the list rather than the check failing. |
| `tests/Janus.Authorization.Tests/Model/AuthorizationModelTests.cs` | The word boundary in the source scan was written `"\b"`, which C# compiles to a backspace, so the pattern matched nothing and the test passed on every input | AUTHZ-MODEL-001 AC1 | A test that cannot fail does not decide its criterion, so the pattern is `"\\b"`; the scan passes with the boundary in force. |
| `tests/Janus.Authorization.Tests/Gate/AccessSeamTests.cs`, `AUTHZ_GATE_001_AC1` | The scan for a public member handing out a set of rows caught `FilterSources<>`, which carries back the sets the host passed in | LIB-HOST-002 | What a host hands the library is the host's own, so handing it back is not the library handing out a set of its rows; the generic type is named as the one exclusion. |
| `src/Janus.Authorization/Gate/AccessGate.cs`, the refusal path | A refusal for a context naming no account has no audit row to correlate to, both identity columns of `janus.audit_records` being accounts | IDN-AUD-001 AC1 | A record that must name both identities cannot be written for a principal that resolves to none, so that refusal carries the code alone and no `correlation` detail. |
| `.github/workflows/gates.yml` | Two rows of the table had no job (`Policy coverage test`, `Truth-table suite`), and the hosting project's integration tests ran in no job | CONV-GATE-001 | One job per row is what makes each row a status check, so the two rows became two jobs and the integration job runs the solution rather than one project. |
| `.github/workflows/gates.yml`, the unit and integration jobs | A test project holding no test of the kind being run ends with exit code 8, which failed both jobs | CONV-GATE-002 AC1, AC2 | The chapter says the checks run, not that every project holds a test of each kind, so the two filtered runs ignore exit code 8 and nothing else. |
| `.github/gates/commit-message.sh` | The length check measured the whole subject line, so a commit whose description was 56 characters failed at 77 | CONV-VCS-003 AC1 | The chapter bounds the description, which is what follows the type and the optional scope, so the prefix is stripped before the line is measured. |
| `.editorconfig`, the five rules the convention places below error | Each scope was written `<directory>/**/*.cs`, which reaches no file sitting at that directory's own root, so CA2007 stood at error in `Janus.Hosting` although the table places it at none there | CONV-SETUP-004 | The file configures what the table states, so each scope is written to reach every file under the directories the table names. |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs`, the mutable-collection scan | The scan read inherited members, so a public delegate failed the check through `Delegate.GetInvocationList` and `Delegate.DynamicInvoke`, which the library does not declare | CONV-CODE-003 AC1 | A contract member is one the library declares, and a public delegate cannot exist without the members the base class library gives it, so the scan reads declared members. |

## 4. Decided in the owner's absence

D-161 makes the owner unavailable for the rest of milestone 1, so every question that
would have stopped this run under the original Tier 2 and Tier 3 rules was decided here
instead. The nine entries below are the entries of `docs/reports/decisions-pending-review.md`,
in the same words; that ledger stands on its own and is what the owner reads first.

The three questions this report first carried are answered by D-161 itself and are no
longer open: a check and a capability page take the same host-supplied rows the filter
takes and refuse without them, a materialised derivation is refreshed by the host inside
its own unit of work, and the reverse lookup over derived grants is the phase 8 view.

### 1. The guide copy of the instruction file is committed

**Phase 2 · 2026-09-19 · Tier 3 · the working guide's sections 8 and 9, D-161**

*The question.* D-161's update replaces the copy of the working guide under
`docs/guide/` and the copy at the repository root, and says the root copy stays excluded
through `.git/info/exclude`, which reads as committing the guide copy. The exclusion in
the clone was the bare file name, which reaches a file of that name at any depth, so
both copies were excluded. Section 5 of the working guide names the other instruction
files in its own text, and sections 8 and 9 forbid letting a tool, model or vendor name
into the repository or its history in absolute terms.

*The readings.*

1. Keep both copies excluded. Nothing of the tooling enters the history, and the guide
   copy can be committed later at no cost once its section 5 is reworded.
2. Commit the guide copy and narrow the exclusion to the root, as the parenthetical
   says, accepting the two names in section 5.

*Chosen: 2.* `docs/decision-log.md`, which the owner wrote and committed at phase 0,
already carries the sentence naming the instruction files at the repository root
(D-149's "Also produced, outside the specification"). The names are therefore already in
the history by the owner's own hand, and the strictest reading of sections 8 and 9,
which bind what this work writes, cannot undo that or make a second occurrence a new
disclosure. D-161 propagates to the guide copy by name, which only a tracked file can
mean. `.git/info/exclude` now holds the root file alone, so the root instruction file
stays excluded and the other instruction files stay excluded at any depth.

*Tests that pin it.* None: this is a repository arrangement, not behaviour. The
exclusion is visible in `git status`, which shows neither root instruction file.

*Chapter text that should change.* The working guide's section 5, second paragraph:
the instruction files excluded are the ones at the repository root, and the versioned
copy under `docs/guide/` is part of the repository. Section 8's ban stands for
everything written from here on.

---

### 2. What makes a path need the host's rows is a derivation that confers what is asked

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-001, D-161 decision 1**

*The question.* D-161 refuses a call without sources "on a type that declares a
derivation". A type declares derivations through its containment as well as its own
declaration, and a derivation confers a role, which allows some permissions and not
others. A deployment that has not written the conferred role at all has no derived grant
to miss. Taken literally, every check on such a type would be refused even where the
derivation could confer nothing.

*The readings.*

1. Refuse whenever the type or a container of it declares a non-materialised derivation,
   whatever the derivation confers.
2. Refuse when a non-materialised derivation reaching the type confers a role that
   allows one of the permissions being asked for.

*Chosen: 2.* This is the condition under which the stored grants are not the whole of
the answer, which is what the refusal exists to prevent (AUTHZ-PRIN-001 AC2). It is the
same predicate the filter already evaluates: `Derivations.ReachingAsync` is what decides
which relationships enter the rule, so the refusal and the rule cannot come apart.
Reading 1 refuses calls whose answer would have been complete, which is not failing
closed but failing loudly for nothing. A materialised derivation is left out of both,
because its grants are rows and are read as rows.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync`,
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckNoDerivationReachesNeedsNoRowsAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-001, the D-161 paragraph: "on a
type a non-materialised derivation reaches, where the role it confers allows the
permission being asked for, a call without sources is refused with
`authz.derivation.sourcesmissing`".

---

### 3. The explanation is refused on a derived type rather than answered from grants alone

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-GATE-004, AUTHZ-DERIVE-001, D-161 decision 1**

*The question.* D-161 names `RequireAsync` and `CapabilitiesAsync` as the two operations
that take the host's rows, and says the point of it is that "no path answers from stored
grants alone". `ExplainAsync` is a third path evaluated over the library's own
connection. On a type a derivation reaches it would state that no grant matched for a
record the filter admits.

*The readings.*

1. Leave `ExplainAsync` as it is. D-161 names two operations and adding a third is
   widening the decision.
2. Give `ExplainAsync` a sources overload as well, and have it name the derivation that
   decided.
3. Refuse `ExplainAsync` with the same fault on a type a derivation reaches, taking no
   new overload.

*Chosen: 3.* Reading 1 leaves a path answering from stored grants alone, which the
chapter sentence forbids. Reading 2 needs a shape for a grant that has no row: an
explanation names a grant by `GrantId`, and a derived grant has none, so it would need a
new public shape and a new sentence in AUTHZ-GATE-004 about what that shape carries;
that is the larger surface and the decision D-161 did not take. Reading 3 refuses rather
than answering wrongly, adds no public surface, and leaves the shape to whoever decides
what a derived grant looks like in an explanation. Concealment is still read first, so a
concealing type answers one way whatever else is true of it (AUTHZ-CONCEAL-003).

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_PRIN_001_AC2_ACheckWithoutTheHostsRowsIsAFaultAsync` asks the
explanation for a disclosing type a derivation reaches and expects the fault.

*Chapter text that should change.* `03` AUTHZ-GATE-004: an explanation is refused with
`authz.derivation.sourcesmissing` on a type a derivation reaches, until a shape exists
for a grant with no row.

---

### 4. A capability page costs one query per permission a derivation confers

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-GATE-005 AC1, D-161 decision 1**

*The question.* AC1 requires a page of fifty records to return capabilities "without
additional queries". A derivation confers a role, and what one role allows is not what
another allows, so a single query cannot say which of several permissions a derived
grant confers on which record.

*The readings.*

1. One query for the page and one further query for every permission a derivation
   confers, none of them growing with the page.
2. One query for the page and one per record, which is the N+1 the item exists to
   prevent.
3. Evaluate no derivation on the page, which is what the item's own reason forbids: a
   page that omits what a derived grant confers hides a control the person may use.

*Chosen: 1.* AC1's reason is the N+1 ("computing them per row in separate calls produces
N+1 queries"), so what it forbids is a cost that grows with the page. One further query
per permission is constant in the page's size and is the fewest queries that can answer
correctly, each of them one statement over the host's own relations.

*Tests that pin it.*
`GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`,
`GateBehaviourTests.AUTHZ_GATE_005_AC2_ADerivedGrantReachesTheCapabilityPageAsync`.

*Chapter text that should change.* `03` AUTHZ-GATE-005 AC1: "A list of 50 records
returns capabilities in one query for the stored grants and one further query per
permission a derivation confers, none of them per record."

---

### 5. The host's query is read where the rule is, not across a port

**Phase 2 · 2026-09-19 · Tier 2 · CONV-DESIGN-003, CONV-LAYOUT-001, LIB-HOST-002**

*The question.* The derived clause on the check and the page paths is a query over the
rows the host supplied, and something must read it. `Janus.Authorization` depends on
`Janus.Core` alone and holds no relational package; the awaiting extension methods are
EF Core's and live in `Janus.Storage`.

*The readings.*

1. Declare a port in `Janus.Authorization` taking the composed query and implement it in
   `Janus.Storage`. CONV-DESIGN-003 forbids an `IQueryable` crossing a port, so the rule
   or the test enforcing it would have to be narrowed to "no port returns one".
2. Reference `Microsoft.EntityFrameworkCore` from `Janus.Authorization`, which
   CONV-LAYOUT-001 gives to `Janus.Storage` alone.
3. Read the query where it is composed, through `IAsyncEnumerable<T>`, which the base
   class library declares and which the host's provider implements.

*Chosen: 3.* It adds no port, no package and no abstraction, and leaves CONV-DESIGN-003
and CONV-LAYOUT-001 exactly as they stand. The query is the host's, composed from the
rows the host supplied and carrying the host's own provider, so reading it issues nothing
of the library's own against a host table (LIB-HOST-002) and takes none of the library's
connections. A queryable that is not asynchronously enumerable raises, naming what the
host must supply, rather than being read synchronously.

*Tests that pin it.* `LibraryStructureTests.CONV_DESIGN_003_AC2_NoPortMethodReturnsAQueryable`
stands unchanged and passes; the derived paths are exercised by `TruthTableTests` and the
derived tests of `GateBehaviourTests`.

*Chapter text that should change.* None. This entry records a design that was chosen to
avoid changing one.

---

### 6. A refresh takes the host's rows and the context, as every other operation does

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005, D-161 decision 2**

*The question.* D-161 gives the signature as
`IDerivationMaterialiser.RefreshAsync(derivationName, resourceId)`. Recomputing the
grants means reading the rows of the relationship for that record, and LIB-HOST-002
forbids the library from querying a host table, so something has to carry the rows.
Every grant also records who granted it and why (AUTHZ-GRANT-002), and `granted_by` is
not nullable.

*The readings.*

1. Take the two arguments literally and find the rows another way: a port the host
   implements, or a queryable registered at startup. Both are a second shape for what
   `FilterSources` already is, and the first is a port the library calls into.
2. Take the same host-supplied sources object decision 1 gives the check and the page,
   and the `AccessContext` every other operation of the library takes, so the refresh is
   `RefreshAsync(context, derivation, resource, sources, cancellationToken)`.

*Chosen: 2.* Decision 1 settled that the rows a derivation is evaluated over are
supplied by the host as `FilterSources`; a second mechanism for the same rows would be
the larger surface and could come apart from the first. The context is not a widening
either: AUTHZ-GATE-001 AC2 has every operation name who is asking, and the grants the
refresh writes record that subject. A context naming no subject is a fault, because the
row it would write cannot be recorded against anybody. The resource is a `ResourceId`
and not a reference, as D-161 has it: the type is the relationship's own.

*Tests that pin it.*
`MaterialisationTests.AUTHZ_DERIVE_005_AC1_TheRowsTheRefreshWroteConferTheRoleAsync`,
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005, the Values paragraph:
"`IDerivationMaterialiser.RefreshAsync(context, derivationName, resourceId, sources)`,
which the host calls from the operation that changes the relationship, inside the same
unit of work, with the same sources object the filter takes."

---

### 7. A materialised derivation writes one grant per record of the type it is declared on

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005 AC1, AUTHZ-DERIVE-002**

*The question.* A derivation is declared on a resource type and follows a relationship
that is about a resource type, and the two need not be the same one. Evaluated, it
reaches records of the declared type that sit at or under the record the relationship
names. Precomputed "as ordinary grants", what record does the grant sit on?

*The readings.*

1. One grant on the record the relationship names. Cheapest, and exact where the
   derivation is declared on the type that relationship is about. Where it is declared
   on a narrower type, the grant reaches records of every other type under that record
   as well, which is more than the derivation confers.
2. One grant on every registered record of the declared type that the relationship's
   record reaches. Exact in both cases, and where the two types are the same it is the
   one grant of reading 1, because a record is its own ancestor at depth zero.
3. Refuse at startup to materialise a derivation declared on a narrower type than its
   relationship, and take reading 1 for the rest.

*Chosen: 2.* Reading 1 confers more than evaluating the derivation would, which is not
failing closed. Reading 3 fails closed but refuses a configuration the evaluated path
supports today, and needs a startup code `10` does not carry. Reading 2 confers exactly
what the derivation confers in both shapes, needs no new code and no refusal, and the
rows it writes are as many as the optimisation ladder says materialisation costs
(AUTHZ-DERIVE-006: a synchronisation problem). Inheritance then carries each grant
exactly as far as it carries the derivation, which is AUTHZ-DERIVE-002.

*Tests that pin it.*
`TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync`, which
runs the whole table against the same deployment with the derivation materialised,
including the case whose fact sits on a container above the record.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005: "the grants are written on
each registered record of the type the derivation is declared on that the record the
relationship names reaches".

---

### 8. What AUTHZ-DERIVE-005 AC3 is proved by

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-005 AC3, D-161 decision 2**

*The question.* AC3 reads "Refresh occurs in the same transaction as the change that
triggers it, or drift is detected and reported." The change that triggers it is a write
to a host table, which the library neither makes nor can enlist: the host's context and
the library's hold their own connections. The sweep D-161 describes cannot supply the
host's rows by itself either, for the same reason the check cannot.

*The readings.*

1. Prove the first clause literally, which needs the library to take part in the host's
   own transaction: a public seam onto the library's connection, or an ambient
   transaction the host's context enlists in. Both are a new public mechanism, and the
   second is a distributed transaction in all but name.
2. Prove what the library can hold itself to: the refresh writes inside the caller's
   unit of work, so a unit of work that is not committed leaves no grant; and a refresh
   run after the relation changed reports the difference and corrects it in the same
   run. The host putting its own write in that unit of work is the host's part, and the
   contract states it.

*Chosen: 2.* AC3 is a disjunction and both of its clauses are now tested. The library
cannot write the host's row (LIB-HOST-002), so "the same transaction" can only mean the
unit of work the host runs the operation in, which is what `IUnitOfWork` already is.
Reading 1 would add a public mechanism for something the host already controls.

*What is left for phase 9.* The sweep itself: something has to run the refresh every
`derivation.materialised.driftcheck` and raise the degradation condition with the
derivation's name in `details`. It needs the host's rows, so it is a job the host hands
its own sources to, and background jobs, named principals and the alert conditions are
phases 4 and 9 of the implementation plan. The setting exists and the refresh reports
the drift; no criterion of AUTHZ-DERIVE-005 waits on it.

*Tests that pin it.*
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync`,
`MaterialisationTests.AUTHZ_DERIVE_005_AC3_ARefreshFindsTheDriftAndCorrectsItAsync`.

*Chapter text that should change.* `03` AUTHZ-DERIVE-005 AC3: "Refresh occurs in the
unit of work the host runs the change in, or the next refresh detects the difference,
reports it and corrects it."

---

### 9. Reverse lookup over derivations is built in phase 8 and nothing of it now

**Phase 2 · 2026-09-19 · Tier 2 · AUTHZ-DERIVE-007, D-161 decision 3**

*The question.* Phase 2 builds "`03` all", and AUTHZ-DERIVE-007 is in `03`. D-161
decision 3 says the reverse lookup over derivations is the phase 8 view and that nothing
of it is built now. This entry records the one item of `03` that phase 2 leaves unbuilt,
because the ledger has to stand alone.

*The readings.* None: the owner settled it in D-161.

*Chosen.* Nothing of the reverse lookup over derived grants is built in phase 2. The
Values paragraph on AUTHZ-DERIVE-007 states how the view will answer, and the view is
phase 8. `authz.reverselookup.budget` exists as a setting from phase 0, which is what
the migration trigger of AUTHZ-SEAM-001 is measured against.

*Tests that pin it.* None of the view. The item's own criteria are the cache criteria,
and those are tested: nothing is keyed on a subject-action-resource triple, a revocation
takes effect on the next request, and an expired grant confers nothing without a sweep.

*Chapter text that should change.* None.

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline runs
`dotnet test` unchanged. The local counts at the end of the phase: `Janus.Core.Tests` 366,
`Janus.Identity.Tests` 62, `Janus.Privacy.Tests` 13, `Janus.Authorization.Tests` 92,
`Janus.Analyzers.Tests` 15, `Janus.Storage.Tests` 153 and `Janus.Hosting.Tests` 99, none
failing.

Full gate: GitHub Actions runs `35465019064` (push) and `35465067470` (pull request) on
branch `phase-02-authorization-2`, pull request #9, green on every job. `Integration tests`,
`Double migration run`, `Destructive-operation detection report`, `Truth-table suite` and
`Dependency vulnerability alerting` run on the pull-request event and `Secret scanning` on
the push event, as CONV-GATE-002 states, so the two runs together are one pass of the
table of CONV-GATE-001.

Pull request #8 carried the same tree on branch `phase-02-authorization`; two of its
commit bodies ran over the 72-character bound of CONV-VCS-003, so the branch was
rebuilt with those two messages corrected and the pull request replaced. The commit
after the two runs above changes this section alone.
