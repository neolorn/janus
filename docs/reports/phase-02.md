# Phase 2: Authorization

Status: stopped at four open questions (section 4). The permission model, the grants,
the roles, the groups, the ancestry closure, the two renderings of one permission rule,
the gate and its explanation are built. Derivations, restriction at the gate, the
capability residuals and the two startup validations that read the database are not;
each waits on a question in section 4.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTHZ-PRIN-001 | AC1, AC2 | `TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync`, `PermissionRuleTests.AUTHZ_PRIN_001_AC2_NoPredicateIsReachableByOnlyOneOfThePaths` |
| AUTHZ-PRIN-002 | AC1, AC2 | `TruthTableTests.AUTHZ_PRIN_002_AC1_TheExpressionTranslatesToOneCorrelatedQueryAsync`, `PermissionRuleTests.AUTHZ_PRIN_002_AC2_NoRenderingSelectsTheRecordsAPrincipalMayReach` |
| AUTHZ-PRIN-003 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_PRIN_003_AC1_AnUndeclaredResourceTypeRaisesAsync`, `GateBehaviourTests.AUTHZ_PRIN_003_AC2_APrincipalThatResolvesToNoAccountIsRefusedAsync`, `SubjectSetsTests.AUTHZ_PRIN_003_AC2_APrincipalWithNoAccountResolvesToNothingAsync`, `AccessSeamTests.AUTHZ_PRIN_003_AC3_NoPathTurnsAFailureIntoAnAllow` |
| AUTHZ-GRANT-001 | AC2, AC3 | `GrantTests.AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganization`, `GrantStoreTests.AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganizationAsync`, `ExplanationTests.AUTHZ_GRANT_001_AC2_AGrantOnARecordConfersNothingOverTheOrganizationAsync`, `PermissionRuleTests.AUTHZ_GRANT_001_AC2_TheOrganizationRenderingReadsNoAncestry`, `GrantTests.AUTHZ_GRANT_001_AC3_OneShapeCarriesAccountAndGroupGrants`, `GrantStoreTests.AUTHZ_GRANT_001_AC3_TheSameTableServesAccountAndGroupGrantsAsync` |
| AUTHZ-GRANT-002 | AC1, AC2, AC3 | `GateBehaviourTests.AUTHZ_GRANT_002_AC1_ADenyOnTheRecordDefeatsAnInheritedAllowAsync`, `GateBehaviourTests.AUTHZ_GRANT_002_AC2_ADenyDefeatsAGrantOnTheWholeOrganizationAsync`, `GateBehaviourTests.AUTHZ_GRANT_002_AC3_RemovingTheDenyRestoresTheAllowAsync` |
| AUTHZ-GRANT-003 | AC1, AC2, AC3 | `GrantStoreTests.AUTHZ_GRANT_003_AC1_AnExpiredGrantIsNotReadWithoutASweepAsync`, `GrantTests.AUTHZ_GRANT_003_AC2_CreationRecordsWhoWhenAndWhy`, `GrantTests.AUTHZ_GRANT_003_AC2_RevocationRecordsWhoWhenAndWhy`, `GrantStoreTests.AUTHZ_GRANT_003_AC2_TheAuditFieldsAreOnTheRowThroughRevocationAsync`, `GrantStoreTests.AUTHZ_GRANT_003_AC3_WhoGrantedThisAndWhenIsAnsweredByQueryAsync` |
| AUTHZ-GRANT-004 | AC1, AC2 | `RoleStoreTests.AUTHZ_GRANT_004_AC1_ARoleAndItsPermissionsAreWrittenAtRuntimeAsync`, `GateBehaviourTests.AUTHZ_GRANT_004_AC2_GrantingTakesEffectOnTheNextRequestAsync` |
| AUTHZ-GROUP-001 | AC1, AC2 | `GroupClosureStoreTests.AUTHZ_GROUP_001_AC1_AUserInATeamInsideADepartmentBelongsToBothAsync`, `GroupClosureStoreTests.AUTHZ_GROUP_001_AC2_NestingDepthIsNotFixedBySchemaAsync`, `SubjectSetsTests.AUTHZ_GROUP_001_AC2_NestingIsFollowedToWhateverDepthIsWrittenAsync` |
| AUTHZ-GROUP-002 | AC1, AC2 | `SubjectSetsTests.AUTHZ_GROUP_002_AC1_TenChecksInOneOperationResolveMembershipOnceAsync`, `SubjectSetsTests.AUTHZ_GROUP_002_AC2_TheResolvedSetIsHeldAndInvalidatedByTheCounterAsync` |
| AUTHZ-INHERIT-001 | AC1, AC2 | `GateBehaviourTests.AUTHZ_INHERIT_001_AC1_AGrantThreeLevelsAboveConfersTheSameAccessAsync`, `GrantStoreTests.AUTHZ_INHERIT_001_AC1_AGrantOnAContainerIsReadForItsContentsAsync`, `GateBehaviourTests.AUTHZ_INHERIT_001_AC2_RemovingTheGrantRemovesTheInheritedAccessAsync` |
| AUTHZ-INHERIT-002 | AC1, AC2, AC3, AC4 | `AncestryStoreTests.AUTHZ_INHERIT_002_AC1_CreatingARecordWritesItsAncestryAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC1_ARollbackLeavesNeitherTheRecordNorItsAncestryAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC2_MovingOutOfEveryContainerClearsWhatWasAboveAsync`, `AncestryStoreTests.AUTHZ_INHERIT_002_AC3_ABulkImportProducesCorrectAncestryAsync`, `PermissionRuleTests.AUTHZ_INHERIT_002_AC4_NoRenderingWalksTheTreeAtRequestTime` |
| AUTHZ-INHERIT-003 | AC1, AC2, AC3 | `AncestryStoreTests.AUTHZ_INHERIT_003_AC1_MovingASubtreeBeneathItsFormerSiblingAsync`, `AncestryStoreTests.AUTHZ_INHERIT_003_AC2_ConcurrentMovesOfOverlappingSubtreesAsync`, `AncestryStoreTests.AUTHZ_INHERIT_003_AC3_NoAncestryRowIsOrphanedAsync` |
| AUTHZ-DERIVE-003 | AC1 | `DerivationTests.AUTHZ_DERIVE_003_AC1_EachDerivationNamesADeclaredRelationship` |
| AUTHZ-DERIVE-005 | AC1, AC2 | `DerivationTests.AUTHZ_DERIVE_005_AC1_MaterialisationIsDeclaredPerDerivation`, `DerivationTests.AUTHZ_DERIVE_005_AC2_WhereAGrantCameFromIsCarriedOnItAndOnItsExplanation`, `GrantStoreTests.AUTHZ_DERIVE_005_AC2_AMaterialisedGrantReadsBackAsOneAsync` |
| AUTHZ-SCOPE-001 | AC2 | `GateBehaviourTests.AUTHZ_SCOPE_001_AC2_AnAccountInTwoOrganizationsReachesOnlyWhatEachGrantsAsync` |
| AUTHZ-MODEL-001 | AC1, AC2, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_001_AC1_NoLibrarySourceNamesAHostDomainType`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC2_TwoDomainsSharingNothingUseTheSameBinary`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC3_AFieldIsNamedByACompilerCheckedReference` |
| AUTHZ-MODEL-002 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_002_AC1_AddingAResourceTypeNeedsNoLibraryChange` |
| AUTHZ-MODEL-003 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_003_AC1_ATypeDeclaredWithoutAPurposeFailsStartup` |
| AUTHZ-MODEL-004 | AC1 for six of the eight conditions, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_004_AC1_EachRefusedDeclarationCarriesItsOwnCode` over `model.containment.cycle`, `model.type.undeclaredreference`, `model.type.noorganizationpath`, `model.purpose.missingassessment` and `model.derivation.undeclaredreference`; the queryable entity with no policy in `AuthorizationModelTests.AUTHZ_MODEL_004_AC3_AnEntityWithoutAPolicyIsFoundByEnumeration` |
| AUTHZ-MODEL-005 | AC1, AC2 | `SerializedModelTests.AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes`, `SerializedModelTests.AUTHZ_MODEL_005_AC2_APermissionModelChangeProducesADiff` |
| AUTHZ-MODEL-006 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_006_AC1_NoRuntimeApiModifiesTheModel` |
| AUTHZ-GATE-001 | AC1, AC2, AC3 | `AccessSeamTests.AUTHZ_GATE_001_AC1_NoPublicSurfaceHandsOutASetOfRows`, `AccessSeamTests.AUTHZ_GATE_001_AC2_EveryOperationNamesWhoIsAsking`, `AuthorizationModelTests.AUTHZ_GATE_001_AC3_EveryQueryableEntityIsEnumeratedAgainstItsPolicy` |
| AUTHZ-GATE-002 | AC1, AC2, AC3 | `PermissionRuleTests.AUTHZ_GATE_002_AC1_EveryRenderingCarriesTheSameValues`, `TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, `TruthTableTests.AUTHZ_GATE_002_AC3_TheFragmentInterpolatesNoValueAsync`, `TruthTableTests.AUTHZ_GATE_002_AC3_AnAliasThatIsNotAnIdentifierIsRefusedAsync` |
| AUTHZ-GATE-003 | AC2 | `DialectContractTests.AUTHZ_GATE_003_AC2_NoClaimOfDialectPortabilityIsDocumented` |
| AUTHZ-GATE-004 | AC1, AC2, AC3, AC4 | `ExplanationTests.AUTHZ_GATE_004_AC1_ADenialNamesThePermissionAndStatesNoGrantMatchedAsync`, `ExplanationTests.AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAndWhatItWasInheritedFromAsync`, `ExplanationTests.AUTHZ_GATE_004_AC3_OnlyATypeThatDisclosesExplainsToTheCallerAsync`, `ExplanationTests.AUTHZ_GATE_004_AC4_ACorrelationIdentifierResolvesOnlyForASupportRoleAsync` |
| AUTHZ-GATE-005 | AC1, AC2 | `GateBehaviourTests.AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync`, `PermissionRuleTests.AUTHZ_GATE_005_AC1_APageIsOneStatementNamingEveryRecordByParameter`, `GateBehaviourTests.AUTHZ_GATE_005_AC2_ACapabilityRequiringNothingFurtherSucceedsAsync` |
| AUTHZ-CONCEAL-001 | AC1, AC2 | `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC1_ATypeDeclaringNothingConceals`, `AuthorizationModelTests.AUTHZ_CONCEAL_001_AC2_DisclosingIsDeclaredAndNotInferred` |
| AUTHZ-CONCEAL-002 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync` |
| AUTHZ-CONCEAL-003 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_003_AC1_TwoRecordsOfOneTypeProduceTheSameRefusalAsync` |
| AUTHZ-CONCEAL-004 | AC1, AC2 | `ExplanationTests.AUTHZ_CONCEAL_004_AC1_TheIdentifierResolvesToTheEntryItWroteAsync`, `ExplanationTests.AUTHZ_CONCEAL_004_AC2_TheIdentifierSaysNothingAboutTheRecordAsync` |
| AUTHZ-CONCEAL-005 | AC1 | `ExplanationTests.AUTHZ_CONCEAL_005_AC1_APermissionTiedToNoRecordIsRefusedAsForbiddenAsync` |
| AUTHZ-CACHE-001 | AC1 to AC7 | `GateBehaviourTests.AUTHZ_CACHE_001_AC1_RevokingTakesEffectOnTheNextRequestAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC1_RevokingAGrantRaisesTheHoldersCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC2_AFailedGrantWriteLeavesTheCounterUnchangedAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC2_ARolledBackMembershipChangeLeavesTheCounterAsync`, `GrantStoreTests.AUTHZ_CACHE_001_AC3_AGroupsGrantRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_AMembershipChangeRaisesEveryTransitiveMemberAsync`, `GroupClosureStoreTests.AUTHZ_CACHE_001_AC3_LeavingAGroupRaisesTheLeaversCounterAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC3_TheHeldSetCarriesTheCounterItWasReadAtAsync`, `SubjectSetsTests.AUTHZ_CACHE_001_AC4_NothingIsKeyedOnASubjectActionAndRecord`, `GateBehaviourTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsTakesEffectAtOnceAsync`, `RoleStoreTests.AUTHZ_CACHE_001_AC5_EditingARolesPermissionsRaisesNoCounterAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC6_MovingARecordTakesEffectAtOnceAsync`, `GateBehaviourTests.AUTHZ_CACHE_001_AC7_AnExpiredGrantConfersNothingWithoutASweepAsync` |
| AUTHZ-IMP-001 | AC1, AC2, AC3 | `AccessSeamTests.AUTHZ_IMP_001_AC1_BothFieldsArePresentOnEveryAccessContext`, `ExplanationTests.AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync`, `AccessSeamTests.AUTHZ_IMP_001_AC3_NoFeatureReadsTheTwoIdentitiesAsDiffering` |
| AUTHZ-TEST-001 | AC2 | `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync` |
| AUTHZ-TEST-002 | AC1, AC2 | `VolumeTests.AUTHZ_TEST_002_AC1_ThePlanIsCapturedAtTheStatedVolumesAsync`, `VolumeTests.AUTHZ_TEST_002_AC2_ThePermissionPredicateUsesAnIndex` |
| AUTHZ-SEAM-001 | AC1, AC2 | `AccessSeamTests.AUTHZ_SEAM_001_AC1_OneTypeStandsBehindTheInterface`, `AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery` |
| CONV-NAME-002 | AC1, AC2 | `AuthorizationModelTests.CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation`, `PermissionsTests.CONV_NAME_002_AC2_NoLibraryOwnedPermissionCarriesAScopeQualifier` |
| LIB-API-001 | AC2 for `10` sections 5.5, 5.6 and 5.11 | `VocabularyContractTests.LIB_API_001_AC2_TheAuthorizationVocabulariesAreTheContract` |
| LIB-API-004 | AC1 | `DialectContractTests.LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific` |
| LIB-HOST-002 | AC1, AC2 | `PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns`, `TruthTableTests.LIB_HOST_002_AC2_TheFilterComposesWithoutMaterialisingRowsAsync` |
| LIB-HOST-004 | AC1 | `AccessSeamTests.LIB_HOST_004_AC1_AuthorizationAloneCompilesAndRuns` |
| LIB-SEAM-001 | AC1 | `AccessSeamTests.LIB_SEAM_001_AC1_ReplacingWhatEvaluatesIsOneChange` |
| LIB-SEAM-002 | AC2 | `AccessSeamTests.LIB_SEAM_002_AC2_NoFeatureReadsActingAndEffectiveAsDiffering` |

The truth table of AUTHZ-TEST-001 is seventeen cases over one shared table, run through
the single check and through the composed filter by
`TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`,
`TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync` and
`TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`.

### Criteria no test decides

| Criterion | How it was verified |
|---|---|
| AUTHZ-GRANT-001 AC1 | Inspection: every permission in `10` section 2.1 and every permission a host declares is a `resource:action` pair, and `Grant` carries subject, role, resource and organization, so each is the item's sentence. A test decides that no second shape exists (`AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery`), not that none could be written. |
| AUTHZ-GATE-003 AC1 | Inspection: `07` LIB-API-004 states the constraint in the library contract document. The API documentation carrying the same constraint is decided by `DialectContractTests.LIB_API_004_AC1_TheFragmentIsDocumentedAsPostgreSqlSpecific`. |
| AUTHZ-MODEL-004 AC2 | By construction: the model is built and validated by `AuthorizationModel.Of` inside `AddJanus`, so a declaration that does not validate stops registration before a container exists to serve a request. |
| AUTHZ-CONCEAL-002 AC2 | By construction, as the criterion itself directs (D-153): one path answers both refusals and the bytes are identical, which `ExplanationTests.AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync` asserts. |
| AUTHZ-DERIVE-003 AC2 | Review step: a derivation whose relationship exists only to grant permission is caught when the declaration is read. |

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTHZ-DERIVE-001, AUTHZ-DERIVE-002, AUTHZ-DERIVE-004, AUTHZ-DERIVE-006, AUTHZ-DERIVE-007, AUTHZ-DERIVE-005 AC3 | Evaluating a derivation reads a fact in a host-owned table on every request | Question 1 |
| AUTHZ-GRANT-001 AC4 | There are no derived grants for a caller to be unable to distinguish | Question 1 |
| AUTHZ-TEST-001 AC1 for the three derived cases, AC3 | The table covers the other eight kinds; the derived rows and the comparison before and after materialisation need the evaluator | Question 1 |
| AUTHZ-MODEL-004 AC1 for `model.derivation.unindexed` | The validation reads the host's index catalogue at startup | Question 1, question 4 |
| AUTHZ-MODEL-004 AC1 for `model.role.undeclaredpermission` | Roles are written at runtime (AUTHZ-GRANT-004), so the validation reads `janus.role_permissions` at startup | Question 4 |
| AUTHZ-GATE-006, AUTHZ-CACHE-001 AC8 | Deciding that a restricted account may read but not modify requires classifying a permission as one or the other | Question 2 |
| AUTHZ-GATE-005 AC3 | What a capability still requires is a residual the gate cannot compute | Question 3 |
| AUTHZ-GATE-005 AC4 | Frontend code | `18`, milestone 2 step 10 |
| AUTHZ-SCOPE-001 AC1 | There is no session | Phase 3 |
| AUTHZ-CACHE-002 AC1 | There is no session cookie and no token | Phase 3 |
| AUTHZ-MODEL-003 AC2 | Sensitivity drives consent behaviour | Phase 7, `04` PRIV-SENS-002 |
| LIB-HOST-004 AC2 | A step-up denial needs the assurance provider seam and a step-up permission | Phase 3, question 3 |
| LIB-SEAM-002 AC1 | The claim is about the whole configuration surface, which the last phase of the milestone completes | Phase 10 |
| `10` section 3, the three administrative roles | Seeded at bootstrap, through the role store built here | Phase 9, OPS-BOOT-001 |

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

## 4. Open questions

### Question 1. Derivations read the host's data, which the contract forbids the library

*Item.* AUTHZ-DERIVE-001 to AUTHZ-DERIVE-007, with LIB-HOST-002 and AUTHZ-TEST-001.

*What the code needs.* A derivation states "whoever holds a named relationship in the
host's own data holds a given role on a given resource type" (AUTHZ-DERIVE-001).
Evaluating one means reading a column of a host-owned table, on the single check and
inside the composed filter alike: AC2 requires that changing the business data changes
access on the next request with no grant written, AC3 that removing the relationship
removes the access, and AUTHZ-DERIVE-002 that a derived grant obeys deny, inheritance
and organization scoping and "composes into the same filter". AUTHZ-DERIVE-004 states
the cost directly: "A derived grant is only as fast as the join it performs into the
host's tables", and its AC1 requires the declared columns to be indexed.

*What the specification says.* LIB-HOST-002: "The library SHALL NOT query host tables",
AC1 "The library contains no query against a host-owned table". D-159, which that item
now carries, says the host maps the two contract tables into its own context and passes
their sets to the filter, and that "the library reads nothing of the host's". The single
check of AUTHZ-PRIN-001 is evaluated by the library over its own connection; the filter
is rendered by the library and composed into the host's query, so a host table named in
the rendering is a query the library wrote against a host-owned table.
`PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns` enforces this.

The two chapters therefore require opposite behaviour of the same evaluation: `03`
requires the library to evaluate a fact in a host-owned table on every request, `07`
forbids the library any query against one. Nothing of the derivation evaluator is built
ahead of the answer; declaring a derivation, its materialisation flag and its provenance
are built and green (AUTHZ-DERIVE-003 AC1, AUTHZ-DERIVE-005 AC1 and AC2).

### Question 2. Restriction needs a permission to be readable or modifying, and nothing says which

*Item.* AUTHZ-GATE-006, with AUTHZ-CACHE-001 AC8, CONV-NAME-002 and `10` section 2.

*What the code needs.* AUTHZ-GATE-006 AC1: "A restricted account's records are readable
by that account and not modifiable", evaluated "wherever the gate is evaluated" (AC2).
The gate is asked for one permission at a time. To admit `document:read` and refuse
`document:edit` for a restricted subject, the gate must know which of the two modifies.

*What the specification says.* CONV-NAME-002 and `10` section 2 fix the `resource:action`
shape and the catalogue of library-owned permissions, and AUTHZ-MODEL-002 lets a host
declare its own permissions by that shape alone. No chapter classifies an action as
reading or modifying, and the model builder takes no such declaration. `10` section 5.20
lists `restricted` among the residuals a capability carries, so the distinction is
expected to be made per permission, but not where it comes from.

This touches the restriction semantics of `01` and `04`, so the report states it and
stops.

### Question 3. Where the `stepup` residual of a capability comes from

*Item.* AUTHZ-GATE-005 AC3, with LIB-HOST-004 AC2, AUTH-STEP-003 AC1 and `10` section 5a.

*What the code needs.* A capability carries what it still requires, per permission:
`{ "can": ["read", "edit"], "requires": { "edit": ["stepup"] } }`. To fill that in, the
gate must know that the host's `document:edit` is gated and the host's `document:read`
is not, and LIB-HOST-004 AC2 requires that without an assurance provider "a step-up
permission is denied, and the denial is distinguishable in diagnostics" (AUTH-STEP-003
AC1 in the same words).

*What the specification says.* `10` section 5a lists the actions that require step-up;
they are the library's own, each tied to an endpoint of `09`, and the section states
that the name "is the key of the policy's `gates` field (section 4.1a)" and "follows the
`resource:action` shape of CONV-NAME-002 but is a gate name, not a permission string
(D-151)". So a gate name is not a permission, yet AUTHZ-GATE-005, LIB-HOST-004 and
AUTH-STEP-003 all speak of a permission that requires step-up. Nothing in the model
builder declares a gate for a host permission, and nothing says a gate name may be
matched against a permission string.

This touches assurance and the step-up gates, so the report states it and stops.

### Question 4. Where a startup validation that must read the database runs

*Item.* AUTHZ-MODEL-004 AC1 and AC2, with AUTHZ-DERIVE-004 AC1, LIB-HOST-001 and
CONV-DESIGN-008.

*What the code needs.* Two of the eight conditions cannot be decided from the
declaration alone. `model.role.undeclaredpermission` compares the permissions of the
roles in `janus.role_permissions`, which are written at runtime (AUTHZ-GRANT-004 AC1),
against the declared catalogue; `model.derivation.unindexed` reads the index catalogue
for the columns a derivation names. Both are asynchronous reads, and AC2 requires the
validation to run before any request is served. `AddJanus` is the synchronous
registration method a host calls (LIB-HOST-001), and it is where the rest of the model
is validated today (`AuthorizationModel.Of`).

*What the specification says.* AUTHZ-MODEL-004 requires validation "at startup, failing
loudly", and that each listed condition produces its named error. LIB-HOST-001 AC1
requires that naming only the declarations starts. CONV-DESIGN-008 fixes the packages.
No chapter says where a validation that needs the database runs.

*The readings.*

1. **`AddJanus` builds a scope and validates synchronously.** Registration stays the one
   entry point and AC2 holds by construction, at the cost of a blocking wait on
   asynchronous work, which JAN0002 (CONV-CODE-008) reports.
2. **A public `ValidateAsync` the host awaits after registration.** No blocking call and
   no new package, at the cost of a public method a host must remember to call, which
   LIB-HOST-001 AC1 argues against, and of AC2 holding only if the host calls it.
3. **A hosted service that validates before the first request.** The platform's own
   order, at the cost of `Microsoft.Extensions.Hosting.Abstractions`, which
   CONV-DESIGN-008 does not name, although the same package is what IDN-LIFE-003a's
   `BackgroundService` needs.

*The smallest fix for each.* For reading 1: a sentence in `07` stating that `AddJanus`
performs the database-backed validation and a `08` line admitting the one blocking call
at registration. For reading 2: a sentence in `07` naming the method and stating that a
host awaits it, and a `10` section 1.5 line for the failure. For reading 3: a row in
CONV-DESIGN-008 for the hosting abstractions and a sentence in `07` stating that
validation runs as a hosted service before the first request.

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

Full gate green on branch `phase-02-authorization`, pull request #8: run `35452903790`
(push), seventeen jobs green with the five pull-request jobs skipped (CONV-GATE-002),
and run `35452905734` (pull request), twenty-one jobs green including `Integration
tests`, `Truth-table suite`, `Policy coverage test`, `Double migration run` and
`Contract tests`; `Secret scanning` runs on the push event and is green there.

Tests: 510 unit, 182 integration, 54 contract, all passing.
