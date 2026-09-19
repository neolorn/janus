# Phase 1: Storage and identity core

Status: stopped at the open questions of section 4. The library's own schema, its first
migration, the account states and their transitions, the per-subject data key with the
field cipher, and the keyed fingerprint are in place and green. The identifier model of
IDN-ACCT-004 to 006, and everything in the phase that rests on it, is not.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| OPS-DATA-001 | AC1 | `LibraryStructureTests.OPS_DATA_001_AC1_NoFileUsesEfCoresRawSqlExecution` |
| OPS-DB-001 | AC1, AC2, AC3 | `SchemaTests.OPS_DB_001_AC1_ArabicSortsPerUnicodeRulesNotByteOrderAsync`, `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_DB_001_AC3_TheDatabaseIsCreatedUnderTheIcuLocaleAsync` |
| OPS-DB-002 | AC1 | `SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync` |
| OPS-MIG-007 | AC1 | `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync` |
| CONV-ENUM-001 | AC1, AC2 | `SchemaTests.CONV_ENUM_001_AC1_AnUnrecognisedStateIsRefusedByTheDatabaseAsync`, `SchemaTests.CONV_ENUM_001_AC2_NoNativeEnumTypeIsInTheSchemaAsync` |
| CONV-TEST-001 | AC1 | The test projects mirror their source projects; `Janus.Identity.Tests`, `Janus.Privacy.Tests` and `Janus.Storage.Tests` are added in this phase |
| `10` sections 5.1, 5.12, 5.12a, 5.12b, 5.12d | The five vocabularies with their wire names | `VocabularyContractTests.LIB_API_001_AC2_TheAccountStatesAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheSuspensionAndDeletionOriginsAreTheContract`, `VocabularyContractTests.LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract` |

Criteria no test can decide, and how each was verified:

| Item | Criterion | Verified by |
|---|---|---|
| OPS-DATA-001 | AC2 | No hand-written SQL exists in the library yet; the accessor it will go through is in place (OPS-DATA-002 in section 2) |
| OPS-DB-002 | AC2 | `JanusDbContext` maps two entities, both in the `janus` schema; the library holds no mapping, connection or query against any other schema |
| OPS-MIG-007 | AC2 | The `Double migration run` job: `.github/gates/double-migration.sh` creates each throwaway database under the ICU provider and applies the migrations, once from empty and once from the previous release's schema; `set -euo pipefail` fails the job on either. No release is tagged, so the second run starts from the empty schema and the script says so |
| CONV-SETUP-004 | AC3 | One suppression is added in this phase: `tests/Janus.Storage.Tests/DatabaseFixture.cs`, CA1515, because the runner constructs the class fixture CONV-TEST-007 requires from outside the assembly, which is the reference the rule looks for and cannot see. It carries its justification in the attribute. The two suppressions of phase 0 stand. No `#pragma warning disable` exists anywhere. Section 4 question 4 asks whether this should be a sixth `.editorconfig` row instead |
| CONV-GATE-002 | AC1, AC2 | The unit and analyser jobs carry no condition; `Integration tests` and `Double migration run` run on the pull-request event and on the default branch only |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| IDN-ACCT-004, IDN-ACCT-005, IDN-ACCT-006 | The canonical form, the mixed-script rule and the PRECIS profiles each need a Unicode implementation the base class library does not provide, and CONV-DESIGN-008 names no package for it | Section 4, questions 1 to 3 |
| REG-IDENT-001, REG-IDENT-002, REG-IDENT-003, REG-PROF-001, REG-PREF-001, REG-ACCT-001 | Identifiers, usernames, display and legal names are canonicalised before they are stored or compared | IDN-ACCT-004 to 006 |
| PRIV-RIGHT-005c | AC2 and AC5 are proven for the keying and for the neutralised value (`FingerprintTests.PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints`, `FingerprintTests.PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes`). AC1, AC3, AC4 and AC6 need the canonical form the fingerprint is computed over, and the erasure operation | IDN-ACCT-004; the erasure operation |
| PRIV-RIGHT-005a | AC3, AC4, AC5, AC6, AC9, AC10 and AC13 are proven (the thirteen tests of `PersonalFieldCipherTests`, `SubjectKeyTests.PRIV_RIGHT_005a_AC9_ErasureOverwritesTheWrappedKey`, `SchemaTests.PRIV_RIGHT_005a_AC4_NoKeyOfAnotherShapeReachesTheTableAsync`). AC1 and AC2 are the host's field declaration and its startup check, which are built with the model builder; AC7, AC8, AC11 and AC12 need a second format, a host table and the read path | Phase 2's model builder; the erasure operation |
| IDN-ACCT-002 | AC1 is proven (`SubjectIdTests.IDN_ACCT_002_AC1_TheIdentifierIsTheDrawnBytesAndNothingElse`). AC2 and AC3 need the erasure operation and the audit trail | IDN-AUD; the erasure operation |
| IDN-ACCT-007 | AC3 and AC4 are proven (`AccountTests`). AC1 is the `NOT NULL` state column and its check constraint, which no account row can yet be written without; AC2 is what a `restricted` account may do, which the authorization seam decides | Phase 2 |
| IDN-LIFE-003 | AC4 is proven for the state half (`AccountTests.IDN_LIFE_003_AC4_ATakedownPassesThroughSuspensionIntoTheWindow`), as are AC5 and AC6. The sessions, the outbox record, the audit line and the second phase are not | Phases 3, 4 and 7 |
| IDN-LIFE-013 | AC1 is proven for the state half (`AccountTests.IDN_LIFE_013_AC1_ReactivationRestoresTheAccount`). Prior access is grants and sessions | Phases 2 and 3 |
| IDN-LIFE-014 | The subject stays resolvable (`AccountTests.IDN_ACCT_007_AC3_ErasureLeavesTheSubjectResolvable`); AC1 needs the erasure operation and AC2 the audit trail | IDN-AUD; the erasure operation |
| OPS-DATA-002 | The accessor and the unit of work are in place; AC1 and AC3 cannot be tested as written | Section 4, question 5 |
| OPS-MIG-001 | AC1 is about the application's startup path, which `Janus.Hosting` does not have yet; AC2 is the deployment pipeline | Phase 10; Milestone 2 |
| `01` section 3, IDN-ORG | An organization's name is canonicalised by IDN-ACCT-004, and membership is reached through a personal email identifier | IDN-ACCT-004 to 006 |
| IDN-ATTR, IDN-PRIN, IDN-AUD, IDN-LIFE-003a, IDN-LIFE-003b, IDN-LIFE-012, IDN-LIFE-015, PRIV-RET-002, PRIV-RET-003, OPS-DB-003, OPS-MIG-002 to OPS-MIG-006 | Not reached; the run ended at the open questions of section 4 | Section 4 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/ResultContractTests.cs` | The test read every public interface of `Janus.Core` as an operation contract, so the LIB-EXT-001 extension point and the CONV-DESIGN-003 persistence port failed it | CONV-DESIGN-005 AC1, which governs the operation contracts of LIB-API-005 | A contract that is not an operation is not an operation contract, so the test names those two and reads the rest as before |
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs` | The test forbade `System.Reflection` anywhere under `src/`, with no allowance for the model builder | CONV-CODE-004 AC2, which forbids reflection outside the model builder and tests | The test excludes the model builder's converter, which is the only shipped file the criterion permits |
| `Directory.Packages.props` | `Microsoft.EntityFrameworkCore` and its design package stood at 10.0.12 while the PostgreSQL provider pins the relational assembly to 10.0.4, so the three did not bind to one relational assembly | CONV-DESIGN-008, whose table names the three as one relational-access set | The set takes the version the provider's own dependency range determines; no package is added or removed |

## 4. Open questions

### 1. `NFKC_Casefold` has no implementation in the base class library and no package in CONV-DESIGN-008

- **Item.** IDN-ACCT-004; `01` section 1.2; D-115; PRIV-RIGHT-005c AC3 and AC4.
- **What the code needs.** Full case folding over compatibility normalisation with default-ignorable code points removed, under a Unicode version pinned in the build and recorded as the canonicalisation version beside every fingerprint.
- **What the specification says.** `01` gives the form as `NFKC_Casefold` (Unicode Standard section 3.13) and says the Unicode version of the implementing library is pinned in the build. D-115 rejects NFC plus invariant lowercase as cheapest with .NET built-ins but a weaker answer to the stated problem.
- **The readings.** `System.String.Normalize` offers FormC, FormD, FormKC and FormKD and no case folding; `System.Globalization` offers no full case fold and nothing that removes default-ignorable code points; the Unicode version is the platform ICU's, which is the machine's rather than a build-time pin. So either the implementing library D-115 speaks of is a package CONV-DESIGN-008 does not yet name, or the mapping tables are generated into `Janus.Core` from the Unicode Character Database at a pinned version by a build step `08` does not name, or the requirement narrows to what the base class library gives, which changes the behaviour IDN-ACCT-004 AC4 describes.
- **The smallest fix for each.** A decision-log entry naming the package, and a row in CONV-DESIGN-008's table; or a decision-log entry naming the generated-table approach and the build step that pins the version; or an amendment to IDN-ACCT-004 and D-115 stating the narrower form.

This decides when two accounts are the same account, so no resolution is proposed.

### 2. The PRECIS profiles have no implementation in the base class library, and the decision log rejected the dependency

- **Item.** IDN-ACCT-004 (`01` section 1.2, usernames and display names); REG-IDENT-001; REG-PROF-001; `20` section 4.
- **What the code needs.** RFC 8265 `UsernameCaseMapped` for usernames and RFC 8266 `Nickname` for display names, both with their derived-property tables.
- **What the specification says.** `01` says usernames take the PRECIS UsernameCaseMapped profile (RFC 8265) and nothing else, and that display names are 1 to 64 bytes under the PRECIS Nickname profile (RFC 8266). D-115 lists PRECIS UsernameCaseMapped under **Rejected**, on the ground that it adds a dependency for little gain.
- **The readings.** Either D-115's rejection is about the canonical form for email addresses, organization names and display-name comparison keys only, and leaves `01`'s username and display-name profiles standing, in which case a package or generated tables are still needed and CONV-DESIGN-008 names neither; or the rejection reaches the profiles too, in which case `01` section 1.2 and `20` section 4 name a profile the log has ruled out.
- **The smallest fix for each.** A decision-log entry saying which of the two readings holds and, if the profiles stand, how they are implemented; or an amendment to `01` section 1.2 and `20` section 4 removing them.

This decides what a username may be, so no resolution is proposed.

### 3. UTS #39 mixed-script detection needs `Script_Extensions`, which the base class library does not expose

- **Item.** IDN-ACCT-005; D-115.
- **What the code needs.** The `Script_Extensions` property of each code point, to decide whether a word is single-script once `Common` and `Inherited` are ignored.
- **What the specification says.** `01` defines single script as UTS #39 section 5.1, mixed-script detection using `Script_Extensions`.
- **The readings.** `System.Globalization.UnicodeCategory` and `System.Text.Rune` give the general category and nothing about script; no base class library type exposes `Script` or `Script_Extensions`. So either a package supplies it, or the property table is generated into `Janus.Core` from the Unicode Character Database at the same pinned version as question 1, or the rule is restated in terms of something the base class library does expose, which is not the rule UTS #39 section 5.1 states.
- **The smallest fix for each.** As question 1, and settled with it, since both rest on one pinned Unicode version.

This decides which display names are rejected, so no resolution is proposed.

### 4. CA1515 fires on every integration fixture, and CONV-SETUP-004 admits two remedies

- **Item.** CONV-SETUP-004; CONV-TEST-007.
- **What the code needs.** CONV-TEST-007 requires integration tests to share one container per test class, which in xunit.v3 is a class fixture. The fixture type carries no test attribute, so CA1515 fires when it is public; making it internal makes CA1812 fire instead, and forces the test class internal too, where CA1812 fires again. No arrangement trips neither.
- **What the specification says.** CONV-SETUP-004 says the `.editorconfig` shall set exactly the listed rules below error and no others, and that any further deviation is a specification defect rather than a local suppression; CA1515 is scoped there to `Janus.Core`, `Janus.Hosting` and `Janus.Conformance`. Its AC3 permits a suppression that carries a justification on the same line and a phase-report entry.
- **The readings.** Either this is the AC3 case, an analyser wrong on a specific line, and each fixture carries its own justified suppression; or it is the sentence's case, a rule the `.editorconfig` should scope to `tests/**` as it already scopes CA1707 and CA2007, which is a specification defect the owner settles. The difference matters because the case recurs at every integration fixture the library will have.
- **The smallest fix for each.** Leave the suppression added in this phase and add one to every later fixture; or add `dotnet_diagnostic.CA1515.severity = none` under `[tests/**/*.cs]` as a sixth row of CONV-SETUP-004's table, and remove the suppression.

### 5. OPS-DATA-002 AC1 and AC3 cannot be tested as written

- **Item.** OPS-DATA-002 AC1 and AC3; CONV-LAYOUT-002 AC1.
- **What the code needs.** A test that writes through `JanusDbContext` inside a transaction and reads the uncommitted write through the OPS-DATA-002 accessor with a hand-written query, across both tools.
- **What the specification says.** OPS-DATA-002 AC3 requires that test. CONV-LAYOUT-002 AC1 permits an area project to grant its internals only to its own test project, to `Janus.Storage`, to `Janus.Hosting` and to `Janus.Cli`, and says any other grant fails the build. Every entity the library maps is internal to an area project.
- **Why it cannot be tested.** `Janus.Storage.Tests` sees `JanusDbContext`, `UnitOfWork` and the accessor, and cannot name `Account` or `SubjectKey`, so it cannot write through the context. `Janus.Identity.Tests` can name `Account` and cannot see the context. No test project can do both, and the grant that would let one is the grant CONV-LAYOUT-002 AC1 forbids.

## 5. Gate result

Fast checks on every commit of the phase: `dotnet build Janus.slnx` with warnings as
errors and analysers at latest-all, `dotnet format Janus.slnx --verify-no-changes`,
`dotnet restore Janus.slnx --locked-mode`, and the unit suites. Green on each.

`dotnet test` still reports that no tests ran on the development machine, as phase 0
records, so the suites were run locally by executing the test binaries. The pipeline
runs `dotnet test` unchanged.

Full gate: GitHub Actions runs `35432791756` (push) and `35432794060` (pull request) on
branch `phase-01-storage`, pull request #4. The pull-request run is green on eighteen
jobs and skips `Secret scanning`, which CONV-GATE-002 runs on the push event only; the
push run is green and skips the four jobs CONV-GATE-002 keeps for a pull request and
the default branch. `Double migration run` and `Integration tests` are added in this
phase and are required status checks on `main`, which now requires nineteen. The commit
carrying this report re-runs both before the merge.

The double migration run was also executed against a container on the development
machine before the pipeline ran, which is how the ICU `CREATE DATABASE` statement, the
`janus_ci` collation and the second run's no-op were first seen.

Tests: 196, all passing. `tests/Janus.Core.Tests` 133, `tests/Janus.Storage.Tests` 26
(18 unit, 8 integration), `tests/Janus.Identity.Tests` 16, `tests/Janus.Analyzers.Tests`
15, `tests/Janus.Privacy.Tests` 6.
