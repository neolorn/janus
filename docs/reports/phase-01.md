# Phase 1: Storage and identity core

Status: stopped at the open question of section 4. The library's own schema and its
three migrations, the account states and their transitions, the per-subject data key
with the field cipher, the keyed fingerprint, the pinned Unicode tables with the
canonical form and the two PRECIS profiles over them, the mixed-script rule, the
settings table, the identifier model, the kind detection, and the identifier and
backup-setting tables with the erasure that neutralises them are in place and green.

The three questions of the previous run are answered by D-155 and are implemented here:
EF Core maps a persistence record per table and never a domain entity, the kind of an
identifier is detected from the value, and `janus_ci` is carried by no column of the
library's schema because no plaintext column a person spells exists in it yet.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| OPS-DATA-001 | AC1 | `LibraryStructureTests.OPS_DATA_001_AC1_NoFileUsesEfCoresRawSqlExecution` |
| OPS-DATA-002 | AC1, AC2, AC3 | `DataConnectionsTests.OPS_DATA_002_AC1_AHandWrittenQuerySeesTheTransactionsOwnWritesAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheAccessorCarriesTheOperationsTransactionAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheConnectionComesBackOpenAsync`, `LibraryStructureTests.OPS_DATA_002_AC2_NoFileButTheAccessorRetrievesAConnection` |
| OPS-DB-001 | AC1, AC2, AC3 | `SchemaTests.OPS_DB_001_AC1_ArabicSortsPerUnicodeRulesNotByteOrderAsync`, `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_DB_001_AC3_TheDatabaseIsCreatedUnderTheIcuLocaleAsync` |
| OPS-DB-002 | AC1 | `SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync` |
| OPS-MIG-007 | AC1 | `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync` |
| CONV-ENUM-001 | AC1, AC2 | `SchemaTests.CONV_ENUM_001_AC1_AnUnrecognisedStateIsRefusedByTheDatabaseAsync`, `SchemaTests.CONV_ENUM_001_AC2_NoNativeEnumTypeIsInTheSchemaAsync` |
| CONV-DESIGN-003 | AC1, AC2, AC3, AC4 | `LibraryStructureTests.CONV_DESIGN_003_AC1_NoAreaReferencesADatabasePackage`, `LibraryStructureTests.CONV_DESIGN_003_AC2_NoPortMethodReturnsAQueryable`, `LibraryStructureTests.CONV_DESIGN_003_AC4_OnlyAPortImplementationReachesTheFieldCipher`, `UnitOfWorkTests.CONV_DESIGN_003_AC3_AnOperationThatFailsBetweenTwoWritesLeavesNeitherAsync`, `UnitOfWorkTests.CONV_DESIGN_003_AC3_TheSameOperationThatSucceedsLeavesBothWritesAsync`, `ModelTests.CONV_DESIGN_003_AC4_NoDomainEntityTypeAppearsInTheModel`, `ModelTests.CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema` |
| CONV-DESIGN-004 | AC1, AC2, AC3 | `LibraryStructureTests.CONV_DESIGN_004_AC1_NoDomainTypeExposesAPropertySetter`, `LibraryStructureTests.CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType`; AC3 by the parse tests of `EmailAddressTests`, `PhoneNumberTests` and `UsernameTests`, each of which refuses an invalid value rather than constructing one |
| CONV-TEST-001 | AC1 | The test projects mirror their source projects; `Janus.Identity.Tests`, `Janus.Privacy.Tests` and `Janus.Storage.Tests` are added in this phase |
| CONV-LAYOUT-001 | AC2, AC3, as D-154 amends the table | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`, `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`, `LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder`, each extended in this phase to the generator project under `tools/` |
| IDN-ACCT-004 | AC1, AC4, AC5, AC6 | `UnicodeConformanceTests.TheCanonicalFormIsTheSameForEveryNormalizationFormOfAString`, `UnicodeConformanceTests.TheNicknameProfileNormalizesToTheConformanceFormKc`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompositionDifferencesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC1_DefaultIgnorableCodePointsAreRemoved`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompatibilityEquivalentsFoldToTheirLetters`, `CanonicalFormTests.IDN_ACCT_004_AC4_FullwidthAndAsciiAddressesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC5_DigitsOfAnyScriptStoreAsTheSameNumber`, `CanonicalFormTests.IDN_ACCT_004_AC5_NothingButADigitIsMapped`, `CanonicalFormTests.IDN_ACCT_004_AC6_ThePinnedUnicodeVersionIsReported`, `EmailAddressTests.IDN_ACCT_004_AC4_OneAddressWhateverFormItWasEnteredIn`, `PhoneNumberTests.IDN_ACCT_004_AC5_OneNumberWhateverScriptItsDigitsWereEnteredIn`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileAcceptsALegalUserpart`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileRefusesAnIllegalUserpart`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileNormalizesToFormC`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileAppliesTheBidiRule`, `PrecisTests.IDN_ACCT_004_AC4_UsernameProfileMapsFullwidthAndHalfwidthCodePoints` |
| IDN-ACCT-005 | AC1, AC2, AC4 | `ScriptMixingTests.IDN_ACCT_005_AC1_AWordMixingCyrillicAndLatinIsRejected`, `ScriptMixingTests.IDN_ACCT_005_AC1_AWordOfOneScriptIsAccepted`, `ScriptMixingTests.IDN_ACCT_005_AC1_AugmentedScriptSetsResolveTheEastAsianCombinations`, `ScriptMixingTests.IDN_ACCT_005_AC1_OneMixedWordRejectsTheWholeValue`, `ScriptMixingTests.IDN_ACCT_005_AC2_SeparateWordsOfDifferentScriptsAreAccepted`, `ScriptMixingTests.IDN_ACCT_005_AC4_CommonScriptCodePointsAreNeverAForeignScript`, `ScriptMixingTests.IDN_ACCT_005_AC4_InheritedScriptCodePointsAreNeverAForeignScript`, `UsernameTests.IDN_ACCT_005_AC1_AUsernameMixingScriptsIsRefused` |
| IDN-ACCT-006 | AC1 (the comparison half) | `CanonicalFormTests.IDN_ACCT_006_AC1_CapitalsDoNotChangeTheCanonicalForm`, `CanonicalFormTests.IDN_ACCT_006_AC1_FoldingIsFullFolding` |
| REG-IDENT-001 | The three kinds and the form each takes | `UsernameTests.REG_IDENT_001_AUsernameTakesTheProfilesForm`, `UsernameTests.REG_IDENT_001_NothingButLettersAndDigitsIsAUsername`, `UsernameTests.REG_IDENT_001_AUsernameIsThreeToThirtyTwoCharacters`, `UsernameTests.REG_IDENT_001_AUsernameSatisfiesTheBidiRule`, `EmailAddressTests.REG_IDENT_001_TheLocalPartIsAtMostSixtyFourOctets`, `EmailAddressTests.REG_IDENT_001_TheWholeAddressIsAtMostTwoHundredAndFiftyFourOctets`, `EmailAddressTests.REG_IDENT_001_NothingButAnAddressIsAnAddress`, `PhoneNumberTests.REG_IDENT_001_ANumberIsAtMostFifteenDigits`, `PhoneNumberTests.REG_IDENT_001_NothingButDigitsIsANumber`, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |
| REG-IDENT-002 | AC1 | `IdentifierSetTests.REG_IDENT_002_AC1_TheFirstVerifiedOfAKindBecomesItsPrimary`, `IdentifierSetTests.REG_IDENT_002_AC1_ExactlyOnePrimaryExistsPerKind`, `IdentifierSetTests.REG_IDENT_002_AC1_EachKindCarriesItsOwnPrimary`, `IdentifierSetTests.REG_IDENT_002_AnAccountHoldsNoMoreOfAKindThanItsMaximum`, `IdentifierSetTests.REG_IDENT_002_TheMaximumIsCountedPerKind` |
| REG-IDENT-003 | The kind detection of D-155 | `IdentifierKindsTests.REG_IDENT_003_AValueCarryingTheSignIsAnEmail`, `IdentifierKindsTests.REG_IDENT_003_AValueThatIsDigitsIsAPhone`, `IdentifierKindsTests.REG_IDENT_003_AnythingElseIsAUsername`, `IdentifierKindsTests.REG_IDENT_003_WhereUsernamesAreOffAnythingElseHasNoKind`, `IdentifierKindsTests.REG_IDENT_003_NoPhoneNumberIsAlsoAUsername`, `IdentifierKindsTests.Detect_SeparatorsAlone_IsNotAPhone` |
| REG-IDENT-009 | The letter rule of D-155 | `UsernameTests.REG_IDENT_009_AUsernameHoldsALetter`, with `identity.username.invalid` in the catalogue (`ErrorCodesTests`) |
| REG-SESS-005 | The one-owner half | `ErasureTests.REG_SESS_005_ALiveFingerprintBelongsToOneAccountAsync` |
| PRIV-RIGHT-005c | AC2, AC3, AC5 | `FingerprintTests.PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints`, `FingerprintTests.PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes`, `ErasureTests.PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync`, `ErasureTests.PRIV_RIGHT_005c_AC5_NeutralisedFingerprintsDoNotCollideAsync`; AC3 is a property of the canonical form the fingerprint is computed over, which the IDN-ACCT-004 tests above decide |
| CONV-SETUP-004 | AC3 | `.editorconfig` carries the sixth row D-154 added; no suppression remains outside the two of phase 0 |
| CONV-GATE-001 | The regenerate-and-diff job | `.github/workflows/gates.yml`, job `Unicode tables regenerate without a diff` |
| `10` sections 5.1, 5.12, 5.12a, 5.12b, 5.12d, 5.17 | The six vocabularies with their wire names | `VocabularyContractTests.LIB_API_001_AC2_TheAccountStatesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheSuspensionAndDeletionOriginsAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| OPS-DATA-001 | AC2 | No hand-written SQL exists in the library yet; the accessor it will go through is in place and is the only source of a connection (OPS-DATA-002 AC2 above) |
| OPS-DB-001 | AC2, the collation's reach | `janus_ci` is created by the migrations and carried by no column of the library's schema. The plaintext columns a person spells, organization names and locked domain names, are phase 8's; every other column is a fingerprint or a ciphertext, through which a collation cannot see (D-155) |
| OPS-DB-002 | AC2 | Every entity type in the model is mapped in the `janus` schema (`ModelTests.CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema`); the library holds no mapping, connection or query against any other schema |
| OPS-MIG-007 | AC2 | The `Double migration run` job: `.github/gates/double-migration.sh` creates each throwaway database under the ICU provider and applies the migrations, once from empty and once from the previous release's schema; `set -euo pipefail` fails the job on either. No release is tagged, so the second run starts from the empty schema and the script says so |
| IDN-ACCT-004 | The canonical form itself | The conformance file of the Unicode Character Database at the pinned version is run in full against the library's own normalization: every line's five forms share one canonical form, and every line whose Form KC column the Nickname profile leaves alone is the form the profile produces from that line's source. The tables the forms run over are regenerated by the `Unicode tables regenerate without a diff` job, which fails if the checked-in tables are not what the pinned version yields |
| IDN-ACCT-004 | AC6, the startup half | The version is pinned in the build, reported (`CanonicalForm.UnicodeVersion`) and recorded on every identifier row (`canonicalisation_version`); the startup comparison against the stored code-point range is the re-derivation of PRIV-RIGHT-005c AC4 |
| IDN-ACCT-005 | AC3 | The named code is `identity.identifier.mixedscript` (`10` section 1.1); it is raised by the identifier service, which is in section 2 below. `ScriptMixing` answers the question the code reports |
| REG-PROF-001 | The Nickname profile of the display name | `PrecisTests.REG_PROF_001_NicknameProfileAcceptsALegalNickname`, `PrecisTests.REG_PROF_001_NicknameProfileSettlesTheSpaces` and `PrecisTests.REG_PROF_001_NicknameProfileDecidesOnTheFreeformClass` prove the profile against the vectors of RFC 8266 section 3. The field's own criteria are in section 2 below |
| CONV-SETUP-004 | AC3 | No suppression is added in this phase; the two of phase 0 stand. No `#pragma warning disable` exists outside the migration files EF Core generates |
| CONV-GATE-002 | AC1, AC2 | The unit and analyser jobs carry no condition; `Integration tests`, `Double migration run` and `Unicode tables regenerate without a diff` run on the pull-request event and on the default branch only |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| IDN-ACCT-004 AC2, AC3 | The row holds both forms; AC2 is what an endpoint returns, and AC3 is the registration, email-change, phone-change and organization-creation operations | Phases 5 and 8 |
| IDN-ACCT-006 AC1, AC2 | Both are about what registration and sign-in do with two spellings of one address, not about what the canonical form is | Phases 3 and 5 |
| REG-IDENT-001 AC1 to AC5 | Each is about a path through registration or an account operation | Phase 5 |
| REG-IDENT-002 AC2, AC3 | Both are notices sent to the security-notice set, which the model computes and the notification pipeline delivers | Phase 4 |
| REG-IDENT-003 AC1, AC2 | Both criteria are answers of `POST /auth/begin`; the kind detection they rest on is implemented and tested above | Phases 3 and 5 |
| REG-IDENT-009 AC1 to AC3 | Registration without a username, the cooling-off and the hold after erasure are account operations | Phase 5 |
| REG-SESS-005 AC1 to AC3 | The response, the owner's notice and the expiring session belong to the registration session | Phases 4 and 5 |
| REG-PROF-001, REG-PREF-001, REG-ACCT-001 | Display name, legal name, date of birth and the declared preferences are personal fields of an account-field table this run did not reach | Section 4 |
| `IIdentifierStore` and its implementation | The port that translates the identifier aggregate to its records and back, which is where the field cipher runs for an identifier. Its integration test cannot be written in any project the grant list of CONV-LAYOUT-002 AC1 permits | Section 4 |
| PRIV-RIGHT-005a | AC3 to AC6, AC9, AC10 and AC13 are proven (the thirteen tests of `PersonalFieldCipherTests`, `SubjectKeyTests.PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey`, `SchemaTests.PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync`, `ErasureTests.PRIV_RIGHT_005c_AC5_ErasureLeavesTheRowsAndNothingReadableAsync`). AC1 and AC2 are the field declaration and its startup check; AC7, AC8, AC11 and AC12 need the read path through the port | Section 4; phase 2's model builder |
| PRIV-RIGHT-005c AC1, AC4, AC6 | Sign-in and duplicate detection are lookups through the port; AC4's re-derivation is a command of `Janus.Cli`; AC6 is restated when the host-side work exists | Section 4; phases 5 and 9 |
| IDN-ACCT-002 | AC1 is proven (`SubjectIdTests.IDN_ACCT_002_AC1_TheIdentifierIsTheDrawnBytesAndNothingElse`). AC2 and AC3 need the erasure operation and the audit trail | IDN-AUD; the erasure operation |
| IDN-ACCT-007 | AC3 and AC4 are proven (`AccountTests`). AC1 is the `NOT NULL` state column and its check constraint, which no account row can be written without; AC2 is what a `restricted` account may do, which the authorization seam decides | Phase 2 |
| IDN-LIFE-003 | AC4 is proven for the state half (`AccountTests.IDN_LIFE_003_AC4_ATakedownPassesThroughSuspensionIntoTheWindow`), as are AC5 and AC6. The sessions, the outbox record, the audit line and the second phase are not | Phases 3, 4 and 7 |
| IDN-LIFE-013 | AC1 is proven for the state half (`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresTheAccount`). Prior access is grants and sessions | Phases 2 and 3 |
| IDN-LIFE-014 | The subject stays resolvable (`AccountTests.IDN_ACCT_007_AC3_ErasureLeavesTheSubjectResolvable`); AC1 needs the erasure operation and AC2 the audit trail | IDN-AUD; the erasure operation |
| OPS-CFG-008 | The settings table is in the library's schema; the store that reads a key through it, the audit of a change and the management endpoints are not | Phases 4 and 5 |
| OPS-MIG-001 | AC1 is about the application's startup path, which `Janus.Hosting` does not have yet; AC2 is the deployment pipeline | Phase 10; Milestone 2 |
| `01` section 3, IDN-ORG, IDN-ATTR, IDN-PRIN, IDN-AUD, IDN-LIFE-003a, IDN-LIFE-003b, IDN-LIFE-012, IDN-LIFE-015, PRIV-RET-002, PRIV-RET-003, OPS-DB-003, OPS-MIG-002 to OPS-MIG-006 | Not reached; the run ended at the open question of section 4, which governs how every one of their ports is verified | Section 4 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/ResultContractTests.cs` | The test read every public interface of `Janus.Core` as an operation contract, so the LIB-EXT-001 extension point and the CONV-DESIGN-003 persistence port failed it | CONV-DESIGN-005 AC1, which governs the operation contracts of LIB-API-005 | A contract that is not an operation is not an operation contract, so the test names those two and reads the rest as before |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | The test forbade `System.Reflection` anywhere under `src/`, with no allowance for the model builder | CONV-CODE-004 AC2, which forbids reflection outside the model builder and tests | The test excludes the model builder's converter, which is the only shipped file the criterion permits |
| `Directory.Packages.props` | `Microsoft.EntityFrameworkCore` and its design package stood at 10.0.12 while the PostgreSQL provider pins the relational assembly to 10.0.4, so the three did not bind to one relational assembly | CONV-DESIGN-008, whose table names the three as one relational-access set | The set takes the version the provider's own dependency range determines; no package is added or removed |
| `tests/Janus.Core.Tests/LibraryStructureTests.cs` | The generator project D-154 adds has no test project, which the grant check read as a project failing to grant its internals to one | CONV-TEST-001 AC1, which requires one test project per **source** project | The generator is under `tools/` and outside the package, so it is not a source project; the check requires a test project only of the projects under `src/`, and the regenerate-and-diff job of CONV-GATE-001 is what proves the generator |
| `src/Janus.Storage/Identity/Identifiers/BackupSettingConfiguration.cs` | `BackupRule.Named` carries no wire spelling, so the setting has no single constrained column to be stored in | `10` section 5.17, which gives the setting as `all-verified`, `primary-only` or the id of one named verified identifier | The row carries the two words in `rule` and the identifier in `named`, with a check that exactly one of the two is present; no spelling the chapter does not give is invented |

## 4. Open questions

### 1. A port implementation cannot be integration-tested in any project the grant list permits

- **Item.** CONV-LAYOUT-002 AC1; CONV-DESIGN-003; CONV-TEST-001, CONV-TEST-002,
  CONV-TEST-007 AC2.
- **What the code needs.** A port implementation in `Janus.Storage` translates an
  aggregate to its records and back, and D-155 makes that translation the one place the
  field cipher runs. An integration test of it names both the aggregate (`Account`,
  `SubjectKey`, `Identifier`, `IdentifierSet`) and the implementation (`AccountStore`,
  `SubjectKeyStore`, `IdentifierStore`).
- **What the specification says.** CONV-LAYOUT-002 AC1 enumerates the permitted
  `InternalsVisibleTo` grants: every non-Core project to its own test project; each
  area project to `Janus.Storage`, `Janus.Hosting` and `Janus.Cli`; `Janus.Core` and
  `Janus.Storage` to `Janus.Hosting` and `Janus.Cli`. Grants are not transitive, so
  `Janus.Storage.Tests` sees the implementation and not the aggregate, an area's test
  project sees the aggregate and not the implementation, and no project sees both.
  CONV-LAYOUT-002 makes every type outside `Janus.Core` and `Janus.Hosting` internal,
  so neither has a public spelling. CONV-TEST-007 AC2 requires every testable criterion
  of an implemented item to have a test carrying its identifier.
- **The readings.** (a) The grant list is missing a line, and a port implementation is
  tested in `Janus.Storage.Tests`, which is where CONV-TEST-001 puts the tests of the
  project the implementations live in. (b) A port implementation is not
  integration-tested on its own at all: the record, the cipher and the fingerprint are
  tested in `Janus.Storage.Tests` as they are in this phase, the aggregate is tested in
  the area's test project, and the translation between them is first exercised end to
  end from `Janus.Hosting.Tests` in phase 5, where the endpoints exist. Each phase
  before that reports the translation as untested by construction.
- **The smallest fix for each.** (a) Add "each area project to `Janus.Storage.Tests`
  (port implementation tests)" to the grant list of CONV-LAYOUT-002 AC1, and the same
  line to `LibraryStructureTests.CONV_LAYOUT_002_AC1_InternalsAreVisibleOnlyWhereThePermittedGrantsSay`.
  (b) A sentence in CONV-TEST-002 or CONV-DESIGN-003 stating that a port implementation
  is verified through the endpoint that uses it and never on its own.

`AccountStore` and `SubjectKeyStore` are written and registered and carry no test of
their own. `IdentifierStore` is not written: a third untested translation, and the one
that runs the field cipher, is not worth adding before the question is answered. This
decides how every persistence port in the library is verified, so no resolution is
proposed.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the unit suites. Green on each.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged.

Full gate: GitHub Actions runs `35432791756` and `35432794060` on branch
`phase-01-storage`, pull request #4; runs `35436793900` (push) and `35436801833`
(pull request) on branch `phase-01-canonicalisation`, pull request #6; and runs
`35438577668` (push) and `35438580241` (pull request) on the same branch, carrying the
persistence records, the kind detection and the identifier table. All six green.
`Unicode tables regenerate without a diff` is added in this phase and becomes the
twentieth required status check on `main`.

Tests: 380, all passing. `tests/Janus.Core.Tests` 296, `tests/Janus.Storage.Tests` 36
(20 unit, 16 integration), `tests/Janus.Identity.Tests` 27, `tests/Janus.Analyzers.Tests`
15, `tests/Janus.Privacy.Tests` 6.
