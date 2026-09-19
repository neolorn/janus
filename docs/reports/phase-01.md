# Phase 1: Storage and identity core

Status: complete. The library's own schema and its eleven migrations, the account
states and their transitions, the per-subject data key with the field cipher, the keyed
fingerprint, the pinned Unicode tables with the canonical form and the two PRECIS
profiles over them, the mixed-script rule, the settings table, the identifier model with
its port, the profile, the photo, the preferences, organizations, memberships, the audit
trail with its partitions and its two maintenance functions, the three database roles
and the erasure are in place and green.

The question of the third run is answered by D-156 and the three of the fourth by D-157,
and all four are implemented here: each area project grants its internals to
`Janus.Storage.Tests`; the photo row stays where it is with its bytes unreadable; the
administrative organization is marked on its own row and refuses its own deletion; and
the roles are `janus_migrate`, `janus_app` and `janus_maintenance`, with the partition
drop taking the two retentions as arguments.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| OPS-DATA-001 | AC1 | `LibraryStructureTests.OPS_DATA_001_AC1_NoFileUsesEfCoresRawSqlExecution` |
| OPS-DATA-002 | AC1, AC2, AC3 | `DataConnectionsTests.OPS_DATA_002_AC1_AHandWrittenQuerySeesTheTransactionsOwnWritesAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheAccessorCarriesTheOperationsTransactionAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheConnectionComesBackOpenAsync`, `LibraryStructureTests.OPS_DATA_002_AC2_NoFileButTheAccessorRetrievesAConnection` |
| OPS-DB-001 | AC1, AC2, AC3 | `SchemaTests.OPS_DB_001_AC1_ArabicSortsPerUnicodeRulesNotByteOrderAsync`, `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_DB_001_AC3_TheDatabaseIsCreatedUnderTheIcuLocaleAsync`, `OrganizationStoreTests.OPS_DB_001_AC2_TheOrganizationNameComparesWithoutRegardToCaseAsync` |
| OPS-DB-002 | AC1 | `SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync` |
| OPS-MIG-007 | AC1 | `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync` |
| CONV-ENUM-001 | AC1, AC2 | `SchemaTests.CONV_ENUM_001_AC1_AnUnrecognisedStateIsRefusedByTheDatabaseAsync`, `SchemaTests.CONV_ENUM_001_AC2_NoNativeEnumTypeIsInTheSchemaAsync`, `SubjectEraserTests.IDN_LIFE_003b_AC3_AnUnrecognisedStatusIsRefusedByTheDatabaseAsync` |
| CONV-DESIGN-003 | AC1, AC2, AC3, AC4 | `LibraryStructureTests.CONV_DESIGN_003_AC1_NoAreaReferencesADatabasePackage`, `LibraryStructureTests.CONV_DESIGN_003_AC2_NoPortMethodReturnsAQueryable`, `LibraryStructureTests.CONV_DESIGN_003_AC4_OnlyAPortImplementationReachesTheFieldCipher`, `UnitOfWorkTests.CONV_DESIGN_003_AC3_AnOperationThatFailsBetweenTwoWritesLeavesNeitherAsync`, `UnitOfWorkTests.CONV_DESIGN_003_AC3_TheSameOperationThatSucceedsLeavesBothWritesAsync`, `ModelTests.CONV_DESIGN_003_AC4_NoDomainEntityTypeAppearsInTheModel`, `ModelTests.CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema` |
| CONV-DESIGN-004 | AC1, AC2, AC3 | `LibraryStructureTests.CONV_DESIGN_004_AC1_NoDomainTypeExposesAPropertySetter`, `LibraryStructureTests.CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType`; AC3 by the parse tests of `EmailAddressTests`, `PhoneNumberTests`, `UsernameTests`, `DisplayNameTests` and `LegalNameTests`, each of which refuses an invalid value rather than constructing one |
| CONV-LAYOUT-002 | AC1, as D-156 amends the grant list | `LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay` |
| CONV-LAYOUT-001 | AC2, AC3, as D-154 amends the table | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`, `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`, `LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder`, each extended in this phase to the generator project under `tools/` |
| CONV-TEST-001 | AC1 | The test projects mirror their source projects; `Janus.Identity.Tests`, `Janus.Privacy.Tests` and `Janus.Storage.Tests` are added in this phase |
| IDN-ACCT-002 | AC1, AC2, AC3 | `SubjectIdTests.IDN_ACCT_002_AC1_TheIdentifierIsTheDrawnBytesAndNothingElse`, `AccountStoreTests.IDN_ACCT_002_AC2_ADeletedAccountsSubjectIsNotIssuedAgainAsync`, `SubjectEraserTests.IDN_ACCT_002_AC3_TheSubjectStillResolvesAfterItsErasureAsync`, `AuditStoreTests.PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync` |
| IDN-ACCT-004 | AC1, AC2, AC4, AC5, AC6 | `UnicodeConformanceTests.TheCanonicalFormIsTheSameForEveryNormalizationFormOfAString`, `UnicodeConformanceTests.TheNicknameProfileNormalizesToTheConformanceFormKc`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompositionDifferencesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC1_DefaultIgnorableCodePointsAreRemoved`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompatibilityEquivalentsFoldToTheirLetters`, `CanonicalFormTests.IDN_ACCT_004_AC4_FullwidthAndAsciiAddressesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC5_DigitsOfAnyScriptStoreAsTheSameNumber`, `CanonicalFormTests.IDN_ACCT_004_AC5_NothingButADigitIsMapped`, `CanonicalFormTests.IDN_ACCT_004_AC6_ThePinnedUnicodeVersionIsReported`, `EmailAddressTests.IDN_ACCT_004_AC4_OneAddressWhateverFormItWasEnteredIn`, `PhoneNumberTests.IDN_ACCT_004_AC5_OneNumberWhateverScriptItsDigitsWereEnteredIn`, `IdentifierStoreTests.IDN_ACCT_004_AC2_TheFormThePersonEnteredIsWhatComesBackAsync`, the five `PrecisTests` of the username profile |
| IDN-ACCT-005 | AC1, AC2, AC4 | The seven `ScriptMixingTests`, `UsernameTests.IDN_ACCT_005_AC1_AUsernameMixingScriptsIsRefused`, `DisplayNameTests.IDN_ACCT_005_AC1_ADisplayNameMixingScriptsWithinAWordIsRefused`, `LegalNameTests.IDN_ACCT_005_AC1_ALegalNameMixingScriptsWithinAWordIsRefused` |
| IDN-ACCT-006 | AC1 | `CanonicalFormTests.IDN_ACCT_006_AC1_CapitalsDoNotChangeTheCanonicalForm`, `CanonicalFormTests.IDN_ACCT_006_AC1_FoldingIsFullFolding`, `IdentifierStoreTests.IDN_ACCT_006_AC1_AnAddressEnteredInAnotherCaseFindsTheAccountHoldingItAsync` |
| IDN-ORG-001 | AC2 | `OrganizationStoreTests.IDN_MEM_002_AC1_TheSchemaTakesMoreThanOneMembershipPerAccountAsync` |
| IDN-ORG-002 | AC1 | `ModelTests.IDN_ORG_002_AC1_NoColumnDistinguishesStaffFromCustomers` |
| IDN-ORG-003 | AC2, AC3, AC4, AC5 | `OrganizationTests.IDN_ORG_003_ADeletionRequestSuspendsTheOrganizationAtOnce`, `OrganizationTests.IDN_ORG_003_AC2_CancellingInsideTheWindowRestoresTheOrganization`, `OrganizationTests.IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapses`, `OrganizationTests.IDN_ORG_003_AC4_TheWindowIsTheConfiguredOne`, `OrganizationTests.IDN_ORG_003_AC5_TheOrganizationSurvivesItsErasure`, `OrganizationStoreTests.IDN_ORG_003_AC2_ACancellationClearsTheWindowOnTheRowAsync`, `OrganizationStoreTests.IDN_ORG_003_AC5_TheRowSurvivesTheErasureAsync` |
| IDN-ORG-004 | AC1, AC2 | `OrganizationTests.IDN_ORG_004_AC1_ADeletionRequestOnTheAdministrativeOrganizationIsRefused`, `OrganizationTests.IDN_ORG_004_AC2_TheMarkIsSetWhereBootstrapSetsItAndNowhereElse`, `OrganizationStoreTests.IDN_ORG_004_AC2_OnlyOneOrganizationCarriesTheMarkAsync`, `OrganizationStoreTests.IDN_ORG_004_TheMarkReadsBackFromTheRowAsync` |
| IDN-MEM-001 | AC1, AC2 | `MembershipTests.IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesNamed`, `MembershipTests.IDN_MEM_001_AC2_TheRecordCarriesItsBeginningAndItsEnd`, `OrganizationStoreTests.IDN_MEM_001_AC1_EndingAMembershipLeavesBothSidesAndTheRowAsync` |
| IDN-MEM-002 | AC1 | `OrganizationStoreTests.IDN_MEM_002_AC1_TheSchemaTakesMoreThanOneMembershipPerAccountAsync` |
| IDN-LIFE-003b | AC1, AC2, AC3, AC4 | `SubjectEraserTests.IDN_LIFE_003b_AC1_NoColumnOfTheAccountCarriesErasureProgressAsync`, `SubjectEraserTests.IDN_LIFE_003b_AC2_EveryIncompleteErasureComesBackInOneQueryAsync`, `SubjectEraserTests.IDN_LIFE_003b_AC3_AnUnrecognisedStatusIsRefusedByTheDatabaseAsync`, `SubjectEraserTests.IDN_LIFE_003b_AC4_TheErasureAndItsRowCommitTogetherAsync`, `SubjectEraserTests.IDN_LIFE_003b_AC4_ATransactionThatRollsBackErasesNothingAsync`, the seven `ErasureTests` of `Janus.Privacy.Tests` |
| IDN-ATTR-003 | AC1, AC2 | `SubjectEraserTests.IDN_ATTR_003_AC1_ThePhotoIsUnreadableInTheSameTransactionAsync`, `LibraryStructureTests.IDN_ATTR_003_AC2_OnlyThePhotosOwnPortReachesTheImageTable`, `ProfilePhotoStoreTests.IDN_ATTR_003_APhotoReadsBackByteForByteAsync` |
| IDN-ATTR-005 | AC1 | `ModelTests.IDN_ATTR_005_AC1_NoLibraryTableHoldsAnAddress` |
| IDN-ATTR-006 | AC1 | `ModelTests.IDN_ATTR_006_AC1_NoSchemaFieldHoldsCoordinates` |
| IDN-ATTR-007 | AC1 | `ModelTests.IDN_ATTR_007_AC1_TheProfileIsTheFourFieldsAndNothingElse` |
| IDN-PRIN-001 | AC1, AC2, AC3 | The eight `SystemPrincipalTests` |
| IDN-PRIN-003 | AC1, AC2, AC3 | `AuditRecordTests.IDN_PRIN_003_AC1_NoPortRemovesARecordOfSomethingThatHappened`, `SubjectEraserTests.PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync`, `SubjectEraserTests.IDN_ACCT_002_AC3_TheSubjectStillResolvesAfterItsErasureAsync`, `ErasureTests.PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync`, `AuditStoreTests.PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync` |
| IDN-AUD-001 | AC1, AC2 | `AuditRecordTests.IDN_AUD_001_AC1_TheRecordNamesBothIdentities`, `AuditRecordTests.IDN_AUD_001_AC2_TheRecordedInstantIsTheOneItOccurredAt`, `AuditRecordTests.IDN_AUD_001_TheOrganizationIsThereWhereOneApplies`, `AuditStoreTests.IDN_AUD_001_AC1_BothIdentityFieldsArePopulatedAsync`, `AuditStoreTests.IDN_AUD_001_ACustomersEventCarriesNoOrganizationAsync` |
| PRIV-RET-002 | AC1, AC2, AC3 (the drop half), AC4, AC5 | `DatabaseRoleTests.PRIV_RET_002_AC1_TheApplicationCannotUpdateOrDeleteAnAuditRowAsync`, `DatabaseRoleTests.PRIV_RET_002_AC3_AnExpiredPartitionIsDroppedAndTheOthersStandAsync`, `DatabaseRoleTests.PRIV_RET_002_AC5_OnlyTheMaintenanceRoleExecutesTheDropAsync`, `DatabaseRoleTests.AuditDropExpiredPartitions_ARetentionBelowItsFloor_IsRefusedAsync`, `AuditStoreTests.PRIV_RET_002_AC2_NoAttributeIsInTheRowInPlainAsync`, `AuditStoreTests.PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync`, `AuditStoreTests.PRIV_RET_002_AC1_TwoEventsOnOneSubjectBothStandAsync`, `AuditStoreTests.PRIV_RET_002_ARecordGoesToThePartitionOfItsCategoryAsync`, `AuditStoreTests.PRIV_RET_002_AC3_TheSweepKeepsThreeMonthsOpenPerCategoryAsync`, `AuditRecordTests.PRIV_RET_002_AnAttributeIsHeldApartFromTheStructuredFields`, `AuditRecordTests.PRIV_RET_002_AC1_TheAuditPortOffersNoWriteButAnAppend` |
| PRIV-RET-003 | AC1, AC2 | `AuditStoreTests.PRIV_RET_003_AC1_TheRecordedInstantIsWhenTheEventOccurredAsync` |
| PRIV-RET-004 | AC1 | `AuditRecordTests.PRIV_RET_004_AC1_WhatHappenedIsACodeAndNotASentence`, `AuditStoreTests.PRIV_RET_004_AC1_TheRowHoldsCodesAndStructuredFieldsAsync` |
| PRIV-RIGHT-005 | AC1, AC4 (the erasure half), AC6 | `SubjectEraserTests.PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync`, `SubjectEraserTests.PRIV_RIGHT_005_AC4_ThePhotoIsRenderedUnreadableByTheSameOperationAsync`, `PreferenceStoreTests.PRIV_RIGHT_005a_AC9_TheValuesOfAnErasedSubjectAreNotReadableAsync`, `ProfileStoreTests.PRIV_RIGHT_005a_AC9_TheProfileOfAnErasedSubjectIsNotReadableAsync` |
| PRIV-RIGHT-005a | AC3, AC4, AC5, AC6, AC8, AC9, AC10, AC12, AC13 | The thirteen `PersonalFieldCipherTests`, `SubjectKeyTests.PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey`, `SchemaTests.PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync`, `SubjectKeyStoreTests.PRIV_RIGHT_005a_AC9_AnErasedKeyLeavesItsFieldsUnrecoverableAsync`, `SubjectKeyStoreTests.PRIV_RIGHT_005a_AC13_AReWrappedKeyReadsTheValuesWrittenBeforeItAsync`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC8_NoFormOfAnIdentifierIsInTheDatabaseInPlainAsync`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC12_ReadingAnAccountsIdentifiersUnwrapsItsKeyOnceAsync`, `IdentifierStoreTests.PRIV_RIGHT_005a_AC9_TheIdentifiersOfAnErasedSubjectAreNotReadableAsync`, `ProfileStoreTests.PRIV_RIGHT_005a_AC8_NoProfileFieldIsInTheDatabaseInPlainAsync`, `ProfilePhotoStoreTests.PRIV_RIGHT_005a_AC8_ThePhotoIsNotInTheDatabaseInPlainAsync`, `ProfilePhotoStoreTests.PRIV_RIGHT_005a_AC9_ThePhotoOfAnErasedSubjectIsNotReadableAsync`, `PreferenceStoreTests.PRIV_RIGHT_005a_AC8_TheDeclaredValuesAreNotInTheDatabaseInPlainAsync` |
| PRIV-RIGHT-005c | AC1, AC2, AC3, AC4, AC5 | `FingerprintTests.PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints`, `FingerprintTests.PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes`, `ErasureTests.PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync`, `ErasureTests.PRIV_RIGHT_005c_AC5_NeutralisedFingerprintsDoNotCollideAsync`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC1_DuplicateDetectionMatchesOnTheKeyedFingerprintAsync`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC4_TheCanonicalisationVersionIsStoredBesideTheFingerprintAsync`, `IdentifierStoreTests.PRIV_RIGHT_005c_AC5_ANeutralisedFingerprintBelongsToNobodyAsync`; AC3 is a property of the canonical form the fingerprint is computed over, which the IDN-ACCT-004 tests above decide |
| OPS-MIG-003 | AC1 | `DatabaseRoleTests.OPS_MIG_003_AC1_TheApplicationAltersNoSchemaAsync` |
| OPS-MIG-003a | AC1, AC2, AC3, AC4 (the subject-key half) | `DatabaseRoleTests.OPS_MIG_003a_AC1_TheMaintenanceRoleAltersNoSchemaAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC2_EachMaintenanceFunctionIsTheMigrationRolesOwnAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC3_TheApplicationExecutesNoMaintenanceFunctionAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC4_TheMaintenanceRoleReachesTheKeysAndNoOtherTableAsync` |
| REG-ACCT-001 | AC2 | `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` |
| REG-IDENT-001 | The three kinds and the form each takes | The four `UsernameTests` of the form, the three `EmailAddressTests` of the bounds, the two `PhoneNumberTests` of the bounds, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |
| REG-IDENT-002 | AC1 | `IdentifierSetTests.REG_IDENT_002_AC1_TheFirstVerifiedOfAKindBecomesItsPrimary`, `IdentifierSetTests.REG_IDENT_002_AC1_ExactlyOnePrimaryExistsPerKind`, `IdentifierSetTests.REG_IDENT_002_AC1_EachKindCarriesItsOwnPrimary`, `IdentifierSetTests.REG_IDENT_002_AnAccountHoldsNoMoreOfAKindThanItsMaximum`, `IdentifierSetTests.REG_IDENT_002_TheMaximumIsCountedPerKind`, `IdentifierStoreTests.RecordAsync_AVerificationAndThePrimaryRole_ReachTheRowsAsync` |
| REG-IDENT-003 | The kind detection of D-155 | The six `IdentifierKindsTests` |
| REG-IDENT-009 | The letter rule of D-155 | `UsernameTests.REG_IDENT_009_AUsernameHoldsALetter`, with `identity.username.invalid` in the catalogue (`ErrorCodesTests`) |
| REG-PREF-001 | AC1, AC2 at the set the endpoint writes through, AC3, the erasure half of AC4 | The five `PreferenceDeclarationsTests` of AC1 and the six of the declaration itself, the six `PreferenceSetTests` of AC2 and AC3 and the six of the set itself, `PreferenceStoreTests.PRIV_RIGHT_005a_AC9_TheValuesOfAnErasedSubjectAreNotReadableAsync` |
| REG-PROF-001 | AC1, and the form each of the four fields takes | `DisplayNameTests.REG_PROF_001_AC1_ADisplayNameIsAtMostSixtyFourBytes`, `DisplayNameTests.REG_PROF_001_AC1_TheBoundIsCountedInBytesAndNotInCharacters`, `DisplayNameTests.REG_PROF_001_ADisplayNameTakesTheProfilesForm`, the four `LegalNameTests` of the form and the bound, `PrecisTests.REG_PROF_001_NicknameProfileAcceptsALegalNickname`, `PrecisTests.REG_PROF_001_NicknameProfileSettlesTheSpaces`, `PrecisTests.REG_PROF_001_NicknameProfileDecidesOnTheFreeformClass`, `ProfileStoreTests.REG_PROF_001_AProfileReadsBackAsItWasWrittenAsync` |
| REG-SESS-005 | The one-owner half | `ErasureTests.REG_SESS_005_ALiveFingerprintBelongsToOneAccountAsync` |
| CONV-SETUP-004 | AC3 | `.editorconfig` carries the sixth row D-154 added; no suppression remains outside the two of phase 0 |
| CONV-GATE-001 | The regenerate-and-diff job | `.github/workflows/gates.yml`, job `Unicode tables regenerate without a diff` |
| `10` sections 5.1, 5.12, 5.12a, 5.12b, 5.12d, 5.17 | The six vocabularies with their wire names | `VocabularyContractTests.LIB_API_001_AC2_TheAccountStatesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheSuspensionAndDeletionOriginsAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |
| `10` section 1.1, five codes | `identity.preference.undeclared`, `identity.preference.wrongtype`, `identity.preference.toolarge`, `identity.preference.administratoronly` and `model.startup.preferencedeclaration` enter the catalogue | `ErrorCodesTests.CONV_NAME_003_AC2_ChangingACodeFailsTheContractTest`, `ErrorCodesTests.CONV_NAME_003_AC1_EveryCodeCarriesMeaningAndRemediation` |

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| OPS-DATA-001 | AC2 | No hand-written SQL exists in the library outside the migrations, which OPS-MIG-007 applies twice; the accessor is the only source of a connection (OPS-DATA-002 AC2 above) |
| OPS-DB-002 | AC2 | Every entity type in the model is mapped in the `janus` schema (`ModelTests.CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema`); the library holds no mapping, connection or query against any other schema |
| OPS-MIG-007 | AC2 | The `Double migration run` job: `.github/gates/double-migration.sh` creates each throwaway database under the ICU provider and applies the migrations, once from empty and once from the previous release's schema; `set -euo pipefail` fails the job on either. No release is tagged, so the second run starts from the empty schema and the script says so |
| IDN-ACCT-003 | AC1 | No operation and no endpoint exists yet. Every port of this phase names one subject, except the audit record, which names the acting and the effective identity IDN-AUD-001 requires and yields a record rather than an account. The criterion is restated over the API surface in phase 5 |
| IDN-ACCT-004 | The canonical form itself | The conformance file of the Unicode Character Database at the pinned version is run in full against the library's own normalization: every line's five forms share one canonical form, and every line whose Form KC column the Nickname profile leaves alone is the form the profile produces from that line's source. The tables the forms run over are regenerated by the `Unicode tables regenerate without a diff` job, which fails if the checked-in tables are not what the pinned version yields |
| IDN-ACCT-004 | AC6, the startup half | The version is pinned in the build, reported (`CanonicalForm.UnicodeVersion`) and recorded on every identifier row (`canonicalisation_version`); the startup comparison against the stored code-point range is the re-derivation of PRIV-RIGHT-005c AC4 |
| IDN-ACCT-005 | AC3 | The named code is `identity.identifier.mixedscript` (`10` section 1.1); it is raised by the identifier service, which is in section 2 below. `ScriptMixing` answers the question the code reports |
| IDN-ATTR-005 | AC2 | No vendor name appears in any namespace of the library: the namespaces are exactly the folders (`LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder`) and every folder is named for a feature of the specification |
| IDN-PRIN-003 | AC1, over the whole left column | No port of this phase offers a removal for an account, an organization, a membership or an audit record; the audit half is decided by `AuditRecordTests.IDN_PRIN_003_AC1_NoPortRemovesARecordOfSomethingThatHappened`. Grants, orders, consents and the sweep of spent artefacts arrive with the phases that build them |
| OPS-MIG-003 | AC2 | The migration credential is a value of the deployment's pipeline, which has no application configuration to be absent from until Milestone 2 step 1. The library names the role and grants against it and holds no credential of any kind |
| REG-PROF-001 | The Nickname profile of the display name | `PrecisTests` proves the profile against the vectors of RFC 8266 section 3 |
| CONV-SETUP-004 | AC3 | No suppression is added in this phase; the two of phase 0 stand. No `#pragma warning disable` exists outside the migration files EF Core generates |
| CONV-GATE-002 | AC1, AC2 | The unit and analyser jobs carry no condition; `Integration tests`, `Double migration run` and `Unicode tables regenerate without a diff` run on the pull-request event and on the default branch only |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| IDN-ACCT-001 | AC1 is what removing a provider credential leaves behind; AC2 is the registration transaction | Phases 3 and 5 |
| IDN-ACCT-003 | AC1 is stated over the API surface, which does not exist | Phase 5 |
| IDN-ACCT-004 AC3 | Re-canonicalising a stored identifier belongs to registration, email change, phone change and organization creation | Phases 5 and 8 |
| IDN-ACCT-006 AC2 | What sign-in does with two spellings of one address | Phase 3 |
| IDN-ACCT-007 | AC3 and AC4 are proven (`AccountTests`). AC1 is the `NOT NULL` state column and its check constraint, which no account row can be written without; AC2 is what a `restricted` account may do, which the authorization seam decides | Phase 2 |
| IDN-ORG-001 AC1 | Organization scoping is an authorization concern | Phase 2 |
| IDN-ORG-002 AC2, AC3 | Both are stated over an organization's factor policy in force | Phases 2 and 3 |
| IDN-ORG-003 AC1 | Halting member access within one request cycle is a session refusal | Phase 3 |
| IDN-ORG-005 | The organization erasure that anonymises what the organization owns needs the audit queries and the grant model; the subject half of the same mechanism is proven above | Phases 2 and 7 |
| IDN-ORG-006 | Domain lock, with its DNS verification and re-check | Phase 8 |
| IDN-MEM-002 AC2, AC3 | Both are stated over the setting that forbids a second membership, read through the settings store | Phase 2 |
| IDN-MEM-003 | Both criteria are about what authorization resolves and what a session carries | Phases 2 and 3 |
| IDN-LIFE-003 | AC4 is proven for the state half (`AccountTests.IDN_LIFE_003_AC4_ATakedownPassesThroughSuspensionIntoTheWindow`), as are AC5 and AC6. The sessions, the outbox record, the audit line and the second phase are not | Phases 3, 4 and 7 |
| IDN-LIFE-003a | The erasures row shares the transaction (proven above); the outbox record it must share it with does not exist | Phase 7 |
| IDN-LIFE-012 | Linking a Google or Apple identity | Phases 3 and 6 |
| IDN-LIFE-013 | AC1 is proven for the state half (`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresTheAccount`). Prior access is grants and sessions | Phases 2 and 3 |
| IDN-LIFE-014 | AC1 is proven (`SubjectEraserTests.PRIV_RIGHT_005_AC6_NoPersonalFieldIsRecoverableAfterErasureAsync`); AC2 is what the account page shows | Phase 5 |
| IDN-LIFE-015 | Propagation to the mail server | Phase 8 |
| IDN-ATTR-001 | The three criteria are the export, the background notification and the fallback across declared languages. The columns the first two read are in place (`PreferenceSetTests.SetLanguage_AndTimeZone_AreHeldWithoutADeclaration`, `PreferenceStoreTests.FindBySubjectAsync_AnErasedSubjectWithNoValues_StillReadsItsLanguageAsync`) | Phases 4, 5 and 7 |
| IDN-ATTR-002 | Availability is an organization policy value | Phase 2 |
| IDN-ATTR-003 AC3 | Serving the photo through the access gate | Phases 2 and 5 |
| IDN-ATTR-004 | Content validation, limits and re-encoding on upload | Phase 5 |
| IDN-ATTR-007 AC2 | What a request accepts and a response carries while the two keys are off | Phase 5 |
| IDN-ATTR-008 | The preferred second-step method, which needs the enrolled factors | Phase 3 |
| IDN-PRIN-001 AC4 | Auditing what a system principal does needs an operation that does something | Phases 2 to 9 |
| IDN-PRIN-002 | Propagation to the mail server | Phase 8 |
| PRIV-RET-002 AC3, the alert half | A missed run of the scheduled job alerts (INF-BG-001), which needs the job and the alert routing | Phase 9 |
| PRIV-RIGHT-005 AC2, AC3, AC5, AC7 | AC2 is an audit query, AC3 the registration transaction, AC5 the host's business records, AC7 the username hold of REG-IDENT-009 | Phases 5 and 7 |
| PRIV-RIGHT-005 AC4, the endpoint half | The bytes are unreadable and the row persists (proven above); what `GET /account/photo` answers is the endpoint's | Phase 5 |
| PRIV-RIGHT-005a AC1, AC2, AC7, AC11 | AC1 and AC2 are the field declaration and its startup check, which the model builder carries; AC7 and AC11 are key-encryption-key rotation and the re-wrap job | Phases 2 and 9 |
| PRIV-RIGHT-005c AC6 | Restated when the host-side work exists | Milestone 2 |
| REG-ACCT-001 AC1, AC3 | AC1 is `GET /account`. AC3's Key half is proven above; its other half is what `04-privacy` says of each field that is not under the key, which the retention work settles | Phases 5 and 7 |
| REG-IDENT-001 AC1 to AC5 | Each is a path through registration or an account operation | Phase 5 |
| REG-IDENT-002 AC2, AC3 | Both are notices sent to the security-notice set, which the notification pipeline delivers | Phase 4 |
| REG-IDENT-003 AC1, AC2 | Both criteria are answers of `POST /auth/begin` | Phases 3 and 5 |
| REG-IDENT-009 AC1 to AC3 | Registration without a username, the cooling-off and the hold after erasure | Phase 5 |
| REG-PREF-001 AC2 at the endpoint, and the export half of AC4 | `PUT /account/preferences` and the subject access export | Phases 5 and 7 |
| REG-PROF-001 AC2, AC3 | Both are stated over a request and a response | Phase 5 |
| REG-SESS-005 AC1 to AC3 | The response, the owner's notice and the expiring session belong to the registration session | Phases 4 and 5 |
| OPS-CFG-008 | The settings table is in the library's schema; the store that reads a key through it, the audit of a change and the management endpoints are not | Phases 4 and 5 |
| OPS-DB-003 | Grant lookup and its partial index | Phase 2 |
| OPS-MIG-001 | AC1 is the application's startup path, which `Janus.Hosting` does not have yet; AC2 is the deployment pipeline | Phase 10; Milestone 2 |
| OPS-MIG-002 | Startup schema verification, which needs the startup path | Phase 10 |
| OPS-MIG-003a AC4, the rotation progress half | The maintenance role reaches the wrapped keys (proven above); the rotation progress table arrives with the re-wrap it records | Phase 9 |
| OPS-MIG-004, OPS-MIG-005, OPS-MIG-006 | Each is stated over the deployment's migration step and its ordering | Milestone 2 step 1 |
| CONV-DESIGN-007 AC1, AC2 | `AddJanus` lives in `Janus.Hosting`, which holds no code; `AddJanusStorage` registers this phase's ports and is called from the test fixtures | Phase 10 |
| CONV-LOG-001 | Every log call goes through a generated method; no log call exists | Phase 3 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/ResultContractTests.cs` | The test read every public interface of `Janus.Core` as an operation contract, so the LIB-EXT-001 extension point and the CONV-DESIGN-003 persistence port failed it | CONV-DESIGN-005 AC1, which governs the operation contracts of LIB-API-005 | A contract that is not an operation is not an operation contract, so the test names those two and reads the rest as before |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | The test forbade `System.Reflection` anywhere under `src/`, with no allowance for the model builder | CONV-CODE-004 AC2, which forbids reflection outside the model builder and tests | The test excludes the model builder's converter, which is the only shipped file the criterion permits |
| `Directory.Packages.props` | `Microsoft.EntityFrameworkCore` and its design package stood at 10.0.12 while the PostgreSQL provider pins the relational assembly to 10.0.4, so the three did not bind to one relational assembly | CONV-DESIGN-008, whose table names the three as one relational-access set | The set takes the version the provider's own dependency range determines; no package is added or removed |
| `tests/Janus.Core.Tests/LibraryStructureTests.cs` | The generator project D-154 adds has no test project, which the grant check read as a project failing to grant its internals to one | CONV-TEST-001 AC1, which requires one test project per **source** project | The generator is under `tools/` and outside the package, so it is not a source project; the check requires a test project only of the projects under `src/`, and the regenerate-and-diff job of CONV-GATE-001 is what proves the generator |
| `src/Janus.Storage/Identity/Identifiers/BackupSettingConfiguration.cs` | `BackupRule.Named` carries no wire spelling, so the setting has no single constrained column to be stored in | `10` section 5.17, which gives the setting as `all-verified`, `primary-only` or the id of one named verified identifier | The row carries the two words in `rule` and the identifier in `named`, with a check that exactly one of the two is present; no spelling the chapter does not give is invented |
| `src/Janus.Storage/JanusDbContext.cs`, the collation | `janus_ci` was created in the library's own schema, and PostgreSQL resolves a column's `COLLATE` clause through the search path, which the library's schema is not on; every write to a collated column failed with `42704` | OPS-DB-001 AC2, which requires the collation to be the one a column compares under | A column names a collation by one identifier, never by a schema and a name, so the collation is created where a column can reach it: the default schema |
| `src/Janus.Storage/Identity/Preferences/PreferenceConfiguration.cs` | REG-PREF-001 gives a typed store of host-declared values and names no column for it, and the field cipher binds one ciphertext to one table and one column | PRIV-RIGHT-005a, together with `10` section 4's `preferences.maxsize`, which caps the whole set rather than a value | A cap stated over the whole set and a cipher bound to one column mean one column holding the whole set, so the declared values are one document under the key |
| `src/Janus.Identity/Preferences/PreferenceSet.cs`, the size | `preferences.maxsize` is bytes (D-152) and the row does not say what is counted | `10` section 4, whose row caps "the whole set of host-declared preference values per account" | The whole set is names with values, so both are counted as UTF-8 bytes and the storage form's own punctuation is not |
| `src/Janus.Storage/Identity/Profiles/ProfileStore.cs`, the date of birth | A date of birth is a personal field and the cipher takes bytes; no chapter gives the encoding a date is ciphered in | PRIV-RIGHT-005a, under which every personal field is bytes under the subject key | The date is written in the extended calendar form of ISO 8601 under the invariant culture, the form `10` already uses for a duration and `12` for an instant |
| `src/Janus.Identity/Organizations/Organization.cs` | IDN-ORG-003 names four stages, and a state vocabulary for them would be a closed set no chapter gives | `10` section 5, which collects every fixed enumeration and names none for an organization | The stages are read from the two instants the item's own table gives them: suspension from the deletion request, erasure from `erased_at`. No state column and no vocabulary is invented |
| `src/Janus.Core/AuditCategory.cs` | PRIV-RET-002 drops a partition when its end is older than "the category's retention", and `10` section 5 collects no audit-category enumeration | `10` section 4.7, which holds exactly two audit retention keys, `retention.audit.security` and `retention.audit.routine` | The categories are the two the retention keys name, spelled as the keys spell them |
| `src/Janus.Storage/Migrations/20260919120118_AddAuditRecords.cs` | PRIV-RET-002 requires partitioning by calendar month and a drop decided by the category's retention, and one partition key cannot carry both | PRIV-RET-002, whose sentence makes the month the unit dropped and the category the thing whose retention decides it | The table is partitioned by list on the category and each category by range on the month, so a month of one category is a partition that can be dropped whole |
| `src/Janus.Core/SystemOperation.cs` | IDN-PRIN-001 AC3 requires an enumerated set of operations and `10` section 5 collects none | IDN-PRIN-001, which names them in its own text: reconciliation, retention purging, records-of-processing generation and expiry sweeps | The set is those four and nothing else |
| `src/Janus.Storage/Migrations/20260919124515_AddDatabaseRoles.cs` | OPS-MIG-003a AC2 requires each maintenance function and AC4 the grants to be "listed in the serialized model output for review", and the serialized model carries tables, columns, keys and indexes and neither a function nor a grant | OPS-MIG-003a, whose point is that both are created by the migration step and reviewable in the repository rather than applied by hand | The migration file is the listing: both functions and every grant and revoke are written there, in one migration, where a reviewer reads them beside the tables they act on |
| `src/Janus.Storage/Migrations/20260919124515_AddDatabaseRoles.cs`, the revokes | PRIV-RET-002 says the drop function is "executable only by" the maintenance role, and PostgreSQL grants execution on a new function to everyone | PRIV-RET-002, whose word is "only" | Execution is taken from everyone and given back to the one role, for both functions |
| `src/Janus.Identity/Organizations/Organization.cs`, `RequestDeletion` | IDN-ORG-004 AC1 requires a refusal carrying `identity.organization.protected`, and the method returned nothing | CONV-DESIGN-005, under which an expected outcome is a `Result` carrying a code, which `PreferenceSet.Set` already is | The method returns a `Result`; the two conditions no chapter names a code for stay exceptions, because they are misuse rather than a refusal |
| `src/Janus.Core/AuditAction.cs` | PRIV-RET-004 requires a code rather than a sentence, and no chapter enumerates the codes an audit record carries | CONV-NAME-003, which fixes the shape of a dotted code, with PRIV-RET-004 | The action is a dotted code of the same shape as an error code, parsed and refused the same way; the set is open, because each phase records the events it adds |

## 4. Open questions

None. The three of the previous run are answered by D-157 and implemented above.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the unit suites. Green on each.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline runs
`dotnet test` unchanged.

Full gate: GitHub Actions runs `35432791756` and `35432794060` on branch
`phase-01-storage`, pull request #4; and, on branch `phase-01-canonicalisation`, pull
request #6, runs `35436793900` and `35436801833`, `35438577668` and `35438580241`,
`35438665246` and `35438667495`, `35442205135` and `35442207078`, `35442572791` and
`35442576373`, `35442749824` and `35442752007`, `35442923295` and `35442924980`, and
finally `35443959810` (push) and `35443961616` (pull request), carrying the
administrative organization, the photo criterion and the three database roles.

The last pull-request run is green on all nineteen jobs it runs, including `Integration
tests`, `Double migration run`, `Unicode tables regenerate without a diff`, `Commit
message format` and `Changelog line present`; `Secret scanning` runs on the push event,
as CONV-GATE-002 states, and the last push run is green. Six earlier pull-request runs
failed `Commit message format` alone, on two messages of 74 characters where
CONV-VCS-003 AC1 allows 72; both were rewritten and the branch force-pushed with the
lease, so the messages in history are
`test(storage): run the account and subject-key ports against a database` and
`- a column names a collation by one identifier, never by a schema`.

Tests: 562, all passing. `tests/Janus.Core.Tests` 348, `tests/Janus.Storage.Tests` 124
(25 unit, 99 integration), `tests/Janus.Identity.Tests` 62,
`tests/Janus.Analyzers.Tests` 15, `tests/Janus.Privacy.Tests` 13.
