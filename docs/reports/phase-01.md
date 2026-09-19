# Phase 1: Storage and identity core

Status: stopped at the open question of section 4. The library's own schema, its first
two migrations, the account states and their transitions, the per-subject data key with
the field cipher, the keyed fingerprint, the pinned Unicode tables with the canonical
form and the two PRECIS profiles over them, the mixed-script rule, the settings table
and the identifier model are in place and green. The identifier table, the profile and
preference fields, and the erasure operation are not: all three are personal fields, and
where a personal field is encrypted in the code is the question of section 4.

The five questions of the previous run are answered by D-154 and are implemented here.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| OPS-DATA-001 | AC1 | `LibraryStructureTests.OPS_DATA_001_AC1_NoFileUsesEfCoresRawSqlExecution` |
| OPS-DATA-002 | AC1, AC2, AC3 | `DataConnectionsTests.OPS_DATA_002_AC1_AHandWrittenQuerySeesTheTransactionsOwnWritesAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheAccessorCarriesTheOperationsTransactionAsync`, `DataConnectionsTests.OPS_DATA_002_AC1_TheConnectionComesBackOpenAsync`, `LibraryStructureTests.OPS_DATA_002_AC2_NoFileButTheAccessorRetrievesAConnection` |
| OPS-DB-001 | AC1, AC2, AC3 | `SchemaTests.OPS_DB_001_AC1_ArabicSortsPerUnicodeRulesNotByteOrderAsync`, `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_DB_001_AC3_TheDatabaseIsCreatedUnderTheIcuLocaleAsync` |
| OPS-DB-002 | AC1 | `SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync` |
| OPS-MIG-007 | AC1 | `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync` |
| CONV-ENUM-001 | AC1, AC2 | `SchemaTests.CONV_ENUM_001_AC1_AnUnrecognisedStateIsRefusedByTheDatabaseAsync`, `SchemaTests.CONV_ENUM_001_AC2_NoNativeEnumTypeIsInTheSchemaAsync` |
| CONV-TEST-001 | AC1 | The test projects mirror their source projects; `Janus.Identity.Tests`, `Janus.Privacy.Tests` and `Janus.Storage.Tests` are added in this phase |
| CONV-LAYOUT-001 | AC2, AC3, as D-154 amends the table | `LibraryStructureTests.CONV_LAYOUT_001_AC2_CoreCarriesNoPackage`, `LibraryStructureTests.CONV_LAYOUT_001_AC3_DependenciesAreExactlyTheOnesTheTableGives`, `LibraryStructureTests.CONV_LAYOUT_003_AC1_EveryNamespaceMatchesItsFolder`, each extended in this phase to the generator project under `tools/` |
| IDN-ACCT-004 | AC1, AC4, AC5, AC6 | `UnicodeConformanceTests.TheCanonicalFormIsTheSameForEveryNormalizationFormOfAString`, `UnicodeConformanceTests.TheNicknameProfileNormalizesToTheConformanceFormKc`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompositionDifferencesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC1_DefaultIgnorableCodePointsAreRemoved`, `CanonicalFormTests.IDN_ACCT_004_AC1_CompatibilityEquivalentsFoldToTheirLetters`, `CanonicalFormTests.IDN_ACCT_004_AC4_FullwidthAndAsciiAddressesShareOneCanonicalForm`, `CanonicalFormTests.IDN_ACCT_004_AC5_DigitsOfAnyScriptStoreAsTheSameNumber`, `CanonicalFormTests.IDN_ACCT_004_AC5_NothingButADigitIsMapped`, `CanonicalFormTests.IDN_ACCT_004_AC6_ThePinnedUnicodeVersionIsReported`, `EmailAddressTests.IDN_ACCT_004_AC4_OneAddressWhateverFormItWasEnteredIn`, `PhoneNumberTests.IDN_ACCT_004_AC5_OneNumberWhateverScriptItsDigitsWereEnteredIn`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileAcceptsALegalUserpart`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileRefusesAnIllegalUserpart`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileNormalizesToFormC`, `PrecisTests.IDN_ACCT_004_AC1_UsernameProfileAppliesTheBidiRule`, `PrecisTests.IDN_ACCT_004_AC4_UsernameProfileMapsFullwidthAndHalfwidthCodePoints` |
| IDN-ACCT-005 | AC1, AC2, AC4 | `ScriptMixingTests.IDN_ACCT_005_AC1_AWordMixingCyrillicAndLatinIsRejected`, `ScriptMixingTests.IDN_ACCT_005_AC1_AWordOfOneScriptIsAccepted`, `ScriptMixingTests.IDN_ACCT_005_AC1_AugmentedScriptSetsResolveTheEastAsianCombinations`, `ScriptMixingTests.IDN_ACCT_005_AC1_OneMixedWordRejectsTheWholeValue`, `ScriptMixingTests.IDN_ACCT_005_AC2_SeparateWordsOfDifferentScriptsAreAccepted`, `ScriptMixingTests.IDN_ACCT_005_AC4_CommonScriptCodePointsAreNeverAForeignScript`, `ScriptMixingTests.IDN_ACCT_005_AC4_InheritedScriptCodePointsAreNeverAForeignScript`, `UsernameTests.IDN_ACCT_005_AC1_AUsernameMixingScriptsIsRefused` |
| IDN-ACCT-006 | AC1 (the comparison half) | `CanonicalFormTests.IDN_ACCT_006_AC1_CapitalsDoNotChangeTheCanonicalForm`, `CanonicalFormTests.IDN_ACCT_006_AC1_FoldingIsFullFolding` |
| REG-IDENT-001 | The three kinds and the form each takes | `UsernameTests.REG_IDENT_001_AUsernameTakesTheProfilesForm`, `UsernameTests.REG_IDENT_001_NothingButLettersAndDigitsIsAUsername`, `UsernameTests.REG_IDENT_001_AUsernameIsThreeToThirtyTwoCharacters`, `UsernameTests.REG_IDENT_001_AUsernameSatisfiesTheBidiRule`, `EmailAddressTests.REG_IDENT_001_TheLocalPartIsAtMostSixtyFourOctets`, `EmailAddressTests.REG_IDENT_001_TheWholeAddressIsAtMostTwoHundredAndFiftyFourOctets`, `EmailAddressTests.REG_IDENT_001_NothingButAnAddressIsAnAddress`, `PhoneNumberTests.REG_IDENT_001_ANumberIsAtMostFifteenDigits`, `PhoneNumberTests.REG_IDENT_001_NothingButDigitsIsANumber`, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |
| REG-IDENT-002 | AC1 | `IdentifierSetTests.REG_IDENT_002_AC1_TheFirstVerifiedOfAKindBecomesItsPrimary`, `IdentifierSetTests.REG_IDENT_002_AC1_ExactlyOnePrimaryExistsPerKind`, `IdentifierSetTests.REG_IDENT_002_AC1_EachKindCarriesItsOwnPrimary`, `IdentifierSetTests.REG_IDENT_002_AnAccountHoldsNoMoreOfAKindThanItsMaximum`, `IdentifierSetTests.REG_IDENT_002_TheMaximumIsCountedPerKind` |
| CONV-SETUP-004 | AC3 | `.editorconfig` carries the sixth row D-154 added; the suppression of the previous run is removed and no suppression remains outside the two of phase 0 |
| CONV-GATE-001 | The regenerate-and-diff job | `.github/workflows/gates.yml`, job `Unicode tables regenerate without a diff` |
| `10` sections 5.1, 5.12, 5.12a, 5.12b, 5.12d, 5.17 | The six vocabularies with their wire names | `VocabularyContractTests.LIB_API_001_AC2_TheAccountStatesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheSuspensionAndDeletionOriginsAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheIdentifierKindsAreTheContract` |

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| OPS-DATA-001 | AC2 | No hand-written SQL exists in the library yet; the accessor it will go through is in place and is the only source of a connection (OPS-DATA-002 AC2 above) |
| OPS-DB-002 | AC2 | `JanusDbContext` maps three entities, all in the `janus` schema; the library holds no mapping, connection or query against any other schema |
| OPS-MIG-007 | AC2 | The `Double migration run` job: `.github/gates/double-migration.sh` creates each throwaway database under the ICU provider and applies the migrations, once from empty and once from the previous release's schema; `set -euo pipefail` fails the job on either. No release is tagged, so the second run starts from the empty schema and the script says so |
| IDN-ACCT-004 | The canonical form itself | The conformance file of the Unicode Character Database at the pinned version is run in full against the library's own normalization: every line's five forms share one canonical form, and every line whose Form KC column the Nickname profile leaves alone is the form the profile produces from that line's source. The tables the forms run over are regenerated by the `Unicode tables regenerate without a diff` job, which fails if the checked-in tables are not what the pinned version yields |
| IDN-ACCT-004 | AC6, the startup half | The version is pinned in the build and reported (`CanonicalForm.UnicodeVersion`); the startup comparison against the stored code-point range is PRIV-RIGHT-005c's re-derivation, which waits on the stored identifiers |
| IDN-ACCT-005 | AC3 | The named code is `identity.identifier.mixedscript` (`10` section 1.1); it is raised by the identifier service, which is in section 2 below. `ScriptMixing` answers the question the code reports |
| REG-PROF-001 | The Nickname profile of the display name | `PrecisTests.REG_PROF_001_NicknameProfileAcceptsALegalNickname`, `PrecisTests.REG_PROF_001_NicknameProfileSettlesTheSpaces` and `PrecisTests.REG_PROF_001_NicknameProfileDecidesOnTheFreeformClass` prove the profile against the vectors of RFC 8266 section 3. The field's own criteria are in section 2 below |
| CONV-SETUP-004 | AC3 | No suppression is added in this phase and the one added in the previous run is removed; the two of phase 0 stand. No `#pragma warning disable` exists anywhere |
| CONV-GATE-002 | AC1, AC2 | The unit and analyser jobs carry no condition; `Integration tests`, `Double migration run` and `Unicode tables regenerate without a diff` run on the pull-request event and on the default branch only |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| IDN-ACCT-004 AC2, AC3 | The form the person entered is retained beside the canonical one, which is an encrypted personal field; AC3 is the registration, email-change, phone-change and organization-creation operations | Section 4; phases 5 and 8 |
| IDN-ACCT-006 AC1, AC2 | Both are about what registration and sign-in do with two spellings of one address, not about what the canonical form is | The identifier table; phases 3 and 5 |
| REG-IDENT-001 AC1 to AC5 | Each is about a path through registration or an account operation | The identifier table; phase 5 |
| REG-IDENT-002 AC2, AC3 | Both are notices sent to the security-notice set, which the model computes and the notification pipeline delivers | Phase 4 |
| REG-IDENT-003 | Both criteria are answers of `POST /auth/begin`. The rule by which the one `identifier` field's kind is detected is not stated anywhere; section 4, question 2 | Section 4; phases 3 and 5 |
| REG-PROF-001, REG-PREF-001, REG-ACCT-001 | Display name, legal name, date of birth and the declared preferences are personal fields under the subject key; the account field table is the sum of them | Section 4 |
| PRIV-RIGHT-005c | AC2 and AC5 are proven for the keying and for the neutralised value (`FingerprintTests.PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints`, `FingerprintTests.PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes`). The canonical form the fingerprint is computed over is now in place; AC1, AC3, AC4 and AC6 need the identifier table and the erasure operation | Section 4 |
| PRIV-RIGHT-005a | AC3, AC4, AC5, AC6, AC9, AC10 and AC13 are proven (the thirteen tests of `PersonalFieldCipherTests`, `SubjectKeyTests.PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey`, `SchemaTests.PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync`). AC1 and AC2 are the field declaration and its startup check; AC7, AC8, AC11 and AC12 need the read path | Section 4; phase 2's model builder |
| IDN-ACCT-002 | AC1 is proven (`SubjectIdTests.IDN_ACCT_002_AC1_TheIdentifierIsTheDrawnBytesAndNothingElse`). AC2 and AC3 need the erasure operation and the audit trail | IDN-AUD; the erasure operation |
| IDN-ACCT-007 | AC3 and AC4 are proven (`AccountTests`). AC1 is the `NOT NULL` state column and its check constraint, which no account row can yet be written without; AC2 is what a `restricted` account may do, which the authorization seam decides | Phase 2 |
| IDN-LIFE-003 | AC4 is proven for the state half (`AccountTests.IDN_LIFE_003_AC4_ATakedownPassesThroughSuspensionIntoTheWindow`), as are AC5 and AC6. The sessions, the outbox record, the audit line and the second phase are not | Phases 3, 4 and 7 |
| IDN-LIFE-013 | AC1 is proven for the state half (`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresTheAccount`). Prior access is grants and sessions | Phases 2 and 3 |
| IDN-LIFE-014 | The subject stays resolvable (`AccountTests.IDN_ACCT_007_AC3_ErasureLeavesTheSubjectResolvable`); AC1 needs the erasure operation and AC2 the audit trail | IDN-AUD; the erasure operation |
| OPS-CFG-008 | The settings table is in the library's schema (`AddSettings`); the store that reads a key through it, the audit of a change and the management endpoints are not | Phases 4 and 5 |
| OPS-MIG-001 | AC1 is about the application's startup path, which `Janus.Hosting` does not have yet; AC2 is the deployment pipeline | Phase 10; Milestone 2 |
| `01` section 3, IDN-ORG, IDN-ATTR, IDN-PRIN, IDN-AUD, IDN-LIFE-003a, IDN-LIFE-003b, IDN-LIFE-012, IDN-LIFE-015, PRIV-RET-002, PRIV-RET-003, OPS-DB-003, OPS-MIG-002 to OPS-MIG-006 | Not reached; the run ended at the open question of section 4 | Section 4 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/ResultContractTests.cs` | The test read every public interface of `Janus.Core` as an operation contract, so the LIB-EXT-001 extension point and the CONV-DESIGN-003 persistence port failed it | CONV-DESIGN-005 AC1, which governs the operation contracts of LIB-API-005 | A contract that is not an operation is not an operation contract, so the test names those two and reads the rest as before |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | The test forbade `System.Reflection` anywhere under `src/`, with no allowance for the model builder | CONV-CODE-004 AC2, which forbids reflection outside the model builder and tests | The test excludes the model builder's converter, which is the only shipped file the criterion permits |
| `Directory.Packages.props` | `Microsoft.EntityFrameworkCore` and its design package stood at 10.0.12 while the PostgreSQL provider pins the relational assembly to 10.0.4, so the three did not bind to one relational assembly | CONV-DESIGN-008, whose table names the three as one relational-access set | The set takes the version the provider's own dependency range determines; no package is added or removed |
| `tests/Janus.Core.Tests/LibraryStructureTests.cs` | The generator project D-154 adds has no test project, which the grant check read as a project failing to grant its internals to one | CONV-TEST-001 AC1, which requires one test project per **source** project | The generator is under `tools/` and outside the package, so it is not a source project; the check requires a test project only of the projects under `src/`, and the regenerate-and-diff job of CONV-GATE-001 is what proves the generator |

## 4. Open questions

### 1. Where a personal field is encrypted in the code is not settled, and the identifier table is the first to need it

- **Item.** PRIV-RIGHT-005a; IDN-ACCT-004 AC2; REG-IDENT-001; REG-ACCT-001; CONV-DESIGN-003; CONV-DESIGN-004.
- **What the code needs.** An identifier row holds the fingerprint of the canonical form and the form the person entered, encrypted under the subject's data key (REG-IDENT-001's storage row, IDN-ACCT-004 AC2). The encryption needs the subject's data key, which is one row per subject and is unwrapped once per operation; the fingerprint needs the fingerprint key. Both keys are `Janus.Storage`'s, and neither can be reached from a value converter, which sees one value and not the row it belongs to.
- **What the specification says.** CONV-DESIGN-003: persistence goes through one port per aggregate, implemented in `Janus.Storage`, and `Janus.Storage` holds one `IEntityTypeConfiguration<T>` per entity in a folder mirroring the area and feature. CONV-DESIGN-004: domain types are plain encapsulated classes with no public setters. PRIV-RIGHT-005a AC12: displaying one subject's details performs one unwrap, not one per field. `Account` and `SubjectKey` are mapped directly today because neither holds a personal field.
- **The readings.** (a) `T` is the domain entity, so the domain entity carries what is stored: the fingerprint and the ciphertext, and no plaintext. The port then takes the values and constructs the entity, because only `Janus.Storage` can encrypt, and a second read shape returns the plaintext for display with one unwrap. (b) `T` is a storage row type beside the domain entity, and the port implementation translates between them, which is a second way of mapping an entity where the repository so far has one. (c) The domain entity carries the plaintext and a port in the area project encrypts and fingerprints, which puts an abstraction in `Janus.Identity` that `08` does not name and gives the area project a cipher it should not have.
- **The smallest fix for each.** (a) A sentence in CONV-DESIGN-003 stating that an entity holding a personal field carries the stored form, and that the read path for display is a port method of its own. (b) A sentence in CONV-DESIGN-003 permitting a storage row type where a field is encrypted, and naming the folder it lives in. (c) A row in CONV-DESIGN-001 or CONV-DESIGN-003 naming the protection port, what it is called and which project declares it.

This decides the shape of every personal field the library will ever store, so no resolution is proposed.

### 2. REG-IDENT-003 requires the kind of an identifier to be detected, and no rule says how

- **Item.** REG-IDENT-003; `20` section 2.1.
- **What the code needs.** `POST /auth/begin` takes one `identifier` field and must decide whether it is an email, a phone or a username before it can canonicalise it, because the three take different canonical forms.
- **What the specification says.** REG-IDENT-003 says the endpoint takes one field, detects its kind, canonicalises it and answers identically whether or not it resolves to an account. Nothing else in `20`, `09` or `10` states the rule.
- **The readings.** The obvious rule is: a value holding an at sign is an email, a value that is a leading plus and digits is a phone, anything else is a username. It is not stated, and it decides behaviour at the sign-in boundary: a value the rule sends to the wrong kind resolves to nothing, and REG-IDENT-003 AC2 requires that answer to be indistinguishable from an unknown identifier, so a wrong rule is silent. A second reading is that the endpoint tries each kind's canonical form in turn and looks each up, which is three lookups per attempt and changes what AC2's timing claim rests on.
- **The smallest fix for each.** A sentence in REG-IDENT-003 giving the detection rule; or a sentence stating that the endpoint attempts each kind in a fixed order and what that order is.

This is at the sign-in boundary and AC2 is a timing claim, so no resolution is proposed.

### 3. OPS-DB-001's stated value names plaintext columns its own criterion says do not exist

- **Item.** OPS-DB-001 AC2; D-153.
- **What the code needs.** To know which columns carry the `janus_ci` collation.
- **What the specification says.** The values paragraph says the collation is "applied to the plaintext columns criterion 2 names". Criterion 2 says database-level case-insensitive comparison applies to columns that remain plaintext, and then that identifiers are fingerprints and a collation cannot see through one, so their case-insensitivity comes from the canonical form. It names no column.
- **The readings.** Either the collation exists for a plaintext column the library has not yet reached (an organization name, a username, a host's own column), in which case the criterion should name it; or the collation is created and applied to nothing in the library's schema, in which case the values paragraph should say so and the criterion is already complete.
- **The smallest fix for each.** Name the columns in criterion 2; or replace "applied to the plaintext columns criterion 2 names" with a sentence stating that the collation is created for the host and for any plaintext identifier column the library later adds.

The collation is created and tested, so nothing is blocked by this; it is reported as a defect.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the unit suites. Green on each.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged.

Full gate: GitHub Actions runs `35432791756` and `35432794060` on branch
`phase-01-storage`, pull request #4, both green; and runs `35436793900` (push) and
`35436801833` (pull request) on branch `phase-01-canonicalisation`, pull request #6.
`Unicode tables regenerate without a diff` is added in this phase and becomes the
twentieth required status check on `main`. The commit carrying this report re-runs
both before the merge.

Tests: 340, all passing. `tests/Janus.Core.Tests` 263, `tests/Janus.Storage.Tests` 29
(18 unit, 11 integration), `tests/Janus.Identity.Tests` 27, `tests/Janus.Analyzers.Tests`
15, `tests/Janus.Privacy.Tests` 6.
