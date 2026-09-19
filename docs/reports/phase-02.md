# Phase 2: Authorization

Status: stopped at three open questions (section 4). The permission model, the grants,
the roles, the groups, the ancestry closure, the two renderings of one permission rule,
the gate and its explanation, the restriction at the gate, the capability residuals,
the derivations on the filter path and the two startup checks that read the database are
built. What a check and a capability page answer on a type with derivations, where a
materialised derivation is refreshed, and what answers reverse lookup over derived
grants each wait on a question in section 4.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTHZ-PRIN-001 | AC1, AC2 | `TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync`, `PermissionRuleTests.AUTHZ_PRIN_001_AC2_NoPredicateIsReachableByOnlyOneOfThePaths` |
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
| AUTHZ-DERIVE-002 | AC1, AC2 | `GateBehaviourTests.AUTHZ_DERIVE_002_AC1_ADenyDefeatsADerivedGrantAsync`, `GateBehaviourTests.AUTHZ_DERIVE_002_AC2_ADerivedGrantOnAContainerReachesItsContentsAsync`, `GateBehaviourTests.AUTHZ_DERIVE_002_AC2_AFactOnAnotherContainerReachesNothingHereAsync` |
| AUTHZ-DERIVE-003 | AC1 | `DerivationTests.AUTHZ_DERIVE_003_AC1_EachDerivationNamesADeclaredRelationship` |
| AUTHZ-DERIVE-004 | AC1, AC2 | `ModelValidationTests.AUTHZ_DERIVE_004_AC1_ADerivationNamingAnUnindexedColumnIsRefusedAsync`, `VolumeTests.AUTHZ_DERIVE_004_AC2_TheDerivedRuleUsesAnIndex` |
| AUTHZ-DERIVE-005 | AC1, AC2 | `DerivationTests.AUTHZ_DERIVE_005_AC1_MaterialisationIsDeclaredPerDerivation`, `DerivationTests.AUTHZ_DERIVE_005_AC2_WhereAGrantCameFromIsCarriedOnItAndOnItsExplanation`, `GrantStoreTests.AUTHZ_DERIVE_005_AC2_AMaterialisedGrantReadsBackAsOneAsync` |
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
| AUTHZ-GATE-005 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`, `PermissionRuleTests.AUTHZ_GATE_005_AC1_APageIsOneStatementNamingEveryRecordByParameter`, `GateBehaviourTests.AUTHZ_GATE_005_AC2_ACapabilityRequiringNothingFurtherSucceedsAsync`, `GateBehaviourTests.AUTHZ_GATE_005_AC3_ACapabilityCarriesWhatTheActionStillRequiresAsync` |
| AUTHZ-GATE-006 | AC1, AC2 | `GateBehaviourTests.AUTHZ_GATE_006_AC1_ARestrictedAccountReadsAndDoesNotModifyAsync`, `AccessSeamTests.AUTHZ_GATE_006_AC2_TheRestrictionIsReadAndRefusedInOnePlace` |
| AUTHZ-CONCEAL-001 | AC1, AC2 | `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC1_ATypeDeclaringNothingConceals`, `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC2_DisclosingIsDeclaredAndNotInferred` |
| AUTHZ-CONCEAL-002 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync` |
| AUTHZ-CONCEAL-003 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_003_AC1_TwoRecordsOfOneTypeProduceTheSameRefusalAsync` |
| AUTHZ-CONCEAL-004 | AC1, AC2 | `ExplanationTests.AUTHZ_CONCEAL_004_AC1_TheIdentifierResolvesToTheEntryItWroteAsync`, `ExplanationTests.AUTHZ_CONCEAL_004_AC2_TheIdentifierSaysNothingAboutTheRecordAsync` |
| AUTHZ-CONCEAL-005 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_005_AC1_APermissionTiedToNoRecordIsRefusedAsForbiddenAsync` |
| AUTHZ-CACHE-001 | AC1 to AC8 | `GateBehaviourTests.AUTHZ_CACHE_001_AC1_RevokingTakesEffectOnTheNextRequestAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC1_RevokingAGrantRaisesTheHoldersCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC2_AFailedGrantWriteLeavesTheCounterUnchangedAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC2_ARolledBackMembershipChangeLeavesTheCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC3_AGroupsGrantRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_AMembershipChangeRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_LeavingAGroupRaisesTheLeaversCounterAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC3_TheHeldSetCarriesTheCounterItWasReadAtAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC4_NothingIsKeyedOnASubjectActionAndRecord`, `GateBehaviourTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsTakesEffectAtOnceAsync`, `RoleStoreTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsRaisesNoCounterAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC6_MovingARecordTakesEffectAtOnceAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC7_AnExpiredGrantConfersNothingWithoutASweepAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC8_RestrictingAnAccountTakesEffectImmediatelyAsync` |
| AUTHZ-IMP-001 | AC1, AC2, AC3 | `AccessSeamTests.AUTHZ_IMP_001_AC1_BothFieldsArePresentOnEveryAccessContext`, `ExplanationTests.AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync`, `AccessSeamTests.AUTHZ_IMP_001_AC3_NoFeatureReadsTheTwoIdentitiesAsDiffering` |
| AUTHZ-TEST-001 | AC2 for the cases the table holds | `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync` |
| AUTHZ-TEST-002 | AC1, AC2 | `VolumeTests.AUTHZ_TEST_002_AC1_ThePlanIsCapturedAtTheStatedVolumesAsync`, `VolumeTests.AUTHZ_TEST_002_AC2_ThePermissionPredicateUsesAnIndex` |
| AUTHZ-SEAM-001 | AC1, AC2 | `AccessSeamTests.AUTHZ_SEAM_001_AC1_OneTypeStandsBehindTheInterface`, `AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery` |
| CONV-NAME-002 | AC1, AC2 | `AuthorizationModelTests.CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation`, `PermissionsTests.CONV_NAME_002_AC2_NoLibraryOwnedPermissionCarriesAScopeQualifier` |
| LIB-API-001 | AC2 for `10` sections 5.5, 5.6 and 5.11 | `VocabularyContractTests.LIB_API_001_AC2_TheAuthorizationVocabulariesAreTheContract` |
| LIB-API-004 | AC1 | `DialectContractTests.LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific` |
| LIB-HOST-002 | AC1, AC2 | `PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns`, `PermissionRuleTests.LIB_HOST_002_AC1_OnlyWhatTheHostRunsNamesTheRelationItDeclared`, `TruthTableTests.LIB_HOST_002_AC2_TheFilterComposesWithoutMaterialisingRowsAsync` |
| LIB-HOST-004 | AC1, AC2 | `AccessSeamTests.LIB_HOST_004_AC1_AuthorizationAloneCompilesAndRuns`, `GateBehaviourTests.LIB_HOST_004_AC2_ABoundActionIsDeniedWithNoAssuranceProviderAsync` |
| LIB-SEAM-001 | AC1 | `AccessSeamTests.LIB_SEAM_001_AC1_ReplacingWhatEvaluatesIsOneChange` |
| LIB-SEAM-002 | AC2 | `AccessSeamTests.LIB_SEAM_002_AC2_NoFeatureReadsActingAndEffectiveAsDiffering` |

The truth table of AUTHZ-TEST-001 is seventeen cases over one shared table, run through
the single check and through the composed filter by
`TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`,
`TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync` and
`TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`. The
three derived cases are not among them; they wait on question 1.

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
| AUTHZ-DERIVE-002 AC3, AUTHZ-TEST-001 AC1 for the three derived cases and AC2 for them | The table runs every case through the single check and the list filter; on a type with a derivation the two paths answer differently, and what the check path answers is question 1 | Question 1 |
| AUTHZ-DERIVE-005 AC3 | The change that triggers a refresh is a write to the host's own relation, which the library never sees | Question 2 |
| AUTHZ-TEST-001 AC3 | No derivation the repository declares is materialised, so the table has one side of the comparison; materialising one needs the refresh of AUTHZ-DERIVE-005 AC3 | Question 2 |
| AUTHZ-DERIVE-007 AC1, AC2 | There is no reverse-lookup operation and no administrative view to report through | Question 3 |
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

## 4. Open questions

### Question 1. What a check and a capability page answer on a type with derivations

*Item.* AUTHZ-PRIN-001 AC1 and AC2, AUTHZ-GATE-005 AC1 and AC2, AUTHZ-DERIVE-002 AC3,
AUTHZ-TEST-001 AC1 and AC2, with D-160 decision 1.

*What the code needs.* Under D-160 a derivation is evaluated in the two renderings the
host executes: the LINQ expression and the SQL fragment, over rows the host passes from
its own context. The single check (`RequireAsync`) and the capability page
(`CapabilitiesAsync`) are evaluated by the library over its own connection, through
renderings that name only `janus.*`. On a type that declares a non-materialised
derivation both therefore answer from stored grants alone: a check refuses a record the
filter admits, and a capability array omits what a derived grant confers. D-160 says a
single check on such a type "runs as the filter applied to the one resource, through the
host's query, never as a library-side read", which says what the host should call; it
does not say what the two library-side operations answer when a host calls them anyway,
and the page path has no filter equivalent at all.

*What the specification says.* AUTHZ-PRIN-001: one rule serves both a single check and a
list filter, AC2 "No permission logic exists that is reachable by only one of the two
paths". AUTHZ-GATE-005: one query answers, for a page of records, what the caller may do
on each. AUTHZ-DERIVE-002 AC3: the truth table covers derived cases "through both check
and filter". LIB-HOST-002 AC1 and D-160: the library queries no host table.

*The readings.*

1. **Neither operation answers for such a type.** Both refuse, fail closed, with a code
   that names the reason, and the host asks through the filter, one record at a time
   where it needs a single check.
2. **Both take the host's rows.** `RequireAsync` and `CapabilitiesAsync` gain an overload
   taking the relationship sources the filter takes, and the library composes the same
   `EXISTS` into a query the host's context executes. `Janus.Authorization` has no way to
   execute an `IQueryable` (it holds no EF Core reference, and `Janus.Storage` cannot run
   it either, the rows being the host's), so this needs an execution seam.
3. **They answer from stored grants alone,** stated in the contract: a deployment that
   declares a derivation uses the filter for that type, and the truth table's derived
   cases run their check column as the filter applied to one record.

*The smallest fix for each.* For reading 1: a row in `10` section 1.5 for the code and a
sentence in `03` AUTHZ-GATE-005 and AUTHZ-PRIN-001. For reading 2: a sentence in `03`
AUTHZ-GATE-002 naming the sources the check and the page take, and a line in `07`
LIB-HOST-002 for the seam that runs them. For reading 3: a sentence in `03`
AUTHZ-DERIVE-001 and AUTHZ-PRIN-001 stating that on a type with derivations the check and
the capability page report stored grants only, and that a single check on such a type is
the filter over one record.

*What is built ahead of the answer.* The filter path is complete and green:
`AUTHZ_DERIVE_001_AC2`, `AUTHZ_DERIVE_001_AC3`, `AUTHZ_DERIVE_002_AC1`, the two
`AUTHZ_DERIVE_002_AC2` cases and `AUTHZ_GRANT_001_AC4` in `GateBehaviourTests`, the two
renderings in `PermissionRuleTests`, and the plan at production volume in `VolumeTests`.
Nothing of the check or the page path was changed.

### Question 2. Where a materialised derivation is refreshed

*Item.* AUTHZ-DERIVE-005 AC3, with AUTHZ-DERIVE-001 and LIB-HOST-002.

*What the code needs.* AC3 requires that refresh "occurs in the same transaction as the
change that triggers it, or drift is detected and reported". The change that triggers it
is a write to the host's own relation. The library never sees that write: it holds no
trigger on a host table, no subscription to one, and no read of one (LIB-HOST-002,
D-160). To refresh in the same transaction the host must call the library from inside its
own transaction; to detect drift, something must read the host's relation and compare it
with the materialised grant rows.

*What the specification says.* AUTHZ-DERIVE-005: materialisation is opt-in per
derivation (AC1, built) and a materialised grant is distinguishable from a written one in
storage and in explanations (AC2, built). Nothing names the operation a host calls to
refresh one, nor the job that detects drift, nor where either is registered. The
background jobs of `06` are phase 9.

*The readings.*

1. **The host refreshes.** The library offers an operation that writes the materialised
   grants of one derivation for one record, which the host calls inside the transaction
   that changed the fact; drift is then impossible rather than detected.
2. **The library detects drift.** A background job compares the materialised rows with
   the host's relation and reports what differs, which needs the host's rows to reach it
   and an alert condition to report through.
3. **Neither is phase 2's.** AC3 waits on the jobs of `06` and phase 9, materialisation
   being an operational decision no deployment has taken.

*The smallest fix for each.* For reading 1: a sentence in `03` AUTHZ-DERIVE-005 naming
the operation and stating that the host calls it in its own transaction. For reading 2: a
sentence naming the job and the sources it takes, and an alert condition in `06`. For
reading 3: a line in the plan's phase 2 row placing AC3 in phase 9.

### Question 3. What answers reverse lookup over derived grants

*Item.* AUTHZ-DERIVE-007 AC1 and AC2, with AUTHZ-GATE-004 and `09` section 8.

*What the code needs.* "Who can access this resource?" over derived grants must evaluate
each declared derivation in reverse: from the record to the subjects holding the
relationship on it or on a container of it. Under D-160 those rows are the host's and
reach the library only as a queryable the host passes at filter time, so an operation
answering this needs the same sources; and the answer must stop at
`authz.reverselookup.budget` and carry `partial` and `unevaluated`. The library has no
reverse-lookup operation, and the view the criteria describe is `GET /admin/access` of
`09` section 8, which the plan builds in phase 8.

*What the specification says.* AUTHZ-DERIVE-007 AC1 and AC2 (D-153) describe the
administrative view's answer. AUTHZ-GATE-004 calls the explanation operation "the backing
query" for that view. The plan gives `03` in full to phase 2 and `09` section 8 to
phase 8.

*The readings.*

1. **Phase 2 owes the operation.** A reverse lookup taking the record and the host's
   relationship rows, reporting stored and derived entries distinctly and stopping at the
   budget, with the endpoint of phase 8 rendering it.
2. **Phase 2 owes nothing here.** The criteria bind the view, which phase 8 builds, and
   the operation behind it is designed with it.

*The smallest fix for each.* For reading 1: a shape in `03` AUTHZ-DERIVE-007 or `07`:
what the operation takes, what it returns, and where the budget is enforced. For
reading 2: a line in the plan's phase 2 row excepting AUTHZ-DERIVE-007 from the phase.

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

The full gate result is recorded with its run identifiers in the commit that follows
this report.
