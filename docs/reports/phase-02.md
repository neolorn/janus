# Phase 2: Authorization

Status: stopped at an open question. The model builder, the model's startup validation
and its serialization, and the permission model's domain types and ports are in place
and green. The access gate is not built: AUTHZ-GATE-002 fixes a public-contract surface
(LIB-API-001, "Permission filter shape") on a point the chapters and the decision log do
not settle, and section 4 states it.

## 1. Items implemented

| Item | Criteria | Tests |
|---|---|---|
| AUTHZ-MODEL-001 | AC1, AC2, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_001_AC1_NoLibrarySourceNamesAHostDomainType`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC2_TwoDomainsSharingNothingUseTheSameBinary`, `AuthorizationModelTests.AUTHZ_MODEL_001_AC3_AFieldIsNamedByACompilerCheckedReference` |
| AUTHZ-MODEL-002 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_002_AC1_AddingAResourceTypeNeedsNoLibraryChange` |
| AUTHZ-MODEL-003 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_003_AC1_ATypeDeclaredWithoutAPurposeFailsStartup` |
| AUTHZ-MODEL-004 | AC1 for five of the eight conditions, AC2, AC3 | `AuthorizationModelTests.AUTHZ_MODEL_004_AC1_EachRefusedDeclarationCarriesItsOwnCode` over `model.containment.cycle`, `model.type.undeclaredreference`, `model.type.noorganizationpath`, `model.purpose.missingassessment` and `model.derivation.undeclaredreference`; `AuthorizationModelTests.AUTHZ_MODEL_004_AC3_AnEntityWithoutAPolicyIsFoundByEnumeration`; AC2 by construction, the model being built by `AuthorizationModel.Of` before anything holds it |
| AUTHZ-MODEL-005 | AC1, AC2 | `SerializedModelTests.AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes`, `SerializedModelTests.AUTHZ_MODEL_005_AC2_APermissionModelChangeProducesADiff`, `SerializedModelTests.WriteTo_ADirectory_WritesTheModelBesideTheBuildsOutputs` |
| AUTHZ-MODEL-006 | AC1 | `AuthorizationModelTests.AUTHZ_MODEL_006_AC1_NoRuntimeApiModifiesTheModel` |
| AUTHZ-GRANT-001 | AC2, AC3 | `GrantTests.AUTHZ_GRANT_001_AC2_AGrantWithNoResourceScopesToTheOrganization`, `GrantTests.AUTHZ_GRANT_001_AC3_OneShapeCarriesAccountAndGroupGrants` |
| AUTHZ-GRANT-003 | AC2 | `GrantTests.AUTHZ_GRANT_003_AC2_CreationRecordsWhoWhenAndWhy`, `GrantTests.AUTHZ_GRANT_003_AC2_RevocationRecordsWhoWhenAndWhy`, with the reason rule of D-153 in `GrantTests.Create_WithoutAStatedReason_RefusedWithReasonRequired`, `GrantTests.Revoke_WithoutAStatedReason_RefusedWithReasonRequired` and `GrantTests.Create_WithAReasonPastTheFreeTextLength_Throws` |
| CONV-NAME-002 | AC1, AC2 | `AuthorizationModelTests.CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation`, `PermissionsTests.CONV_NAME_002_AC2_NoLibraryOwnedPermissionCarriesAScopeQualifier`, with the pattern itself in the thirteen `PermissionTests` and the catalogue of chapter 10 section 2.1 in `PermissionsTests.All_TheCatalogue_HoldsEveryDeclaredPermission` |
| LIB-API-001 | AC2 for chapter 10 sections 5.5, 5.6 and 5.11 | `VocabularyContractTests.LIB_API_001_AC2_TheAuthorizationVocabulariesAreTheContract` |

The expiry of AUTHZ-GRANT-003 AC1 is read where a grant is read, in `Grant.IsLive`, and
covered by `GrantTests.IsLive_AroundItsExpiry_FollowsTheInstant`,
`GrantTests.IsLive_WithNoExpiry_IsLive` and
`GrantTests.IsLive_AfterRevocation_IsNotLive`. The criterion itself is a statement about
the gate and is not claimed here.

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| AUTHZ-PRIN-001, AUTHZ-PRIN-002, AUTHZ-PRIN-003 | The gate is not built | Section 4 |
| AUTHZ-GRANT-001 AC1 and AC4, AUTHZ-GRANT-002, AUTHZ-GRANT-003 AC1 and AC3, AUTHZ-GRANT-004 | Decided by the gate and by the grant store, neither built | Section 4 |
| AUTHZ-GROUP-001, AUTHZ-GROUP-002 | The group closure is a storage concern whose shape follows from how the filter reads it | Section 4 |
| AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-INHERIT-003 | The ancestry closure is public contract (LIB-API-001), and how the two renderings reach it is the open question | Section 4 |
| AUTHZ-DERIVE-001 to AUTHZ-DERIVE-007 | Declarable already; evaluating a derivation is the gate | Section 4 |
| AUTHZ-SCOPE-001 | Carried on every registered resource; enforced by the gate | Section 4 |
| AUTHZ-MODEL-003 AC2 | Sensitivity drives consent behaviour, which is phase 7 | `04` PRIV-SENS-002 |
| AUTHZ-MODEL-004 AC1 for `model.role.undeclaredpermission` and `model.derivation.unindexed` | The first needs the role store, the second needs the host's index catalogue read at startup | AUTHZ-GRANT-004, AUTHZ-DERIVE-004 |
| AUTHZ-GATE-001 to AUTHZ-GATE-006 | The gate is not built | Section 4 |
| AUTHZ-CONCEAL-001 to AUTHZ-CONCEAL-005 | Decided at the gate's boundary | Section 4 |
| AUTHZ-CACHE-001, AUTHZ-CACHE-002 | What is cached is the grant rows and the group set the gate reads | Section 4 |
| AUTHZ-IMP-001 | `AccessContext` carries both identities; AC2 and AC3 are assertions about audit and about every feature, claimed when the gate writes audit | Section 4 |
| AUTHZ-TEST-001, AUTHZ-TEST-002 | The truth table runs every case through both renderings | Section 4 |
| AUTHZ-SEAM-001, LIB-SEAM-001, LIB-SEAM-002 | The single interface is the gate | Section 4 |
| LIB-API-004, LIB-HOST-002, LIB-HOST-004 | The fragment renderer, the host's composition and the assurance provider are all the gate | Section 4 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Janus.Core.Tests/PublicSurfaceTests.cs`, the `ModelBuilder` list | `src/Janus.Core/DeclaredMember.cs` reads the member a host names in a declaration lambda and so uses `System.Reflection`, which the list did not admit | CONV-CODE-004 AC2 | The criterion leaves reflection to the model builder, and the declared-member reader is part of the model builder, so its file joins the list rather than the check failing. |
| `tests/Janus.Authorization.Tests/Model/AuthorizationModelTests.cs`, line 43 | The word boundary in the source scan was written `"\b"`, which C# compiles to a backspace, so the pattern matched nothing and the test passed on every input | AUTHZ-MODEL-001 AC1 | A test that cannot fail does not decide its criterion, so the pattern is `"\\b"`; the scan passes with the boundary in force. |

## 4. Open questions

**AUTHZ-GATE-002, with LIB-API-001, LIB-PKG-002 and LIB-HOST-002. What the expression
rendering is a predicate over, and how it reaches the ancestry closure.**

*What the code needs.* One rule definition and two renderers. The fragment renderer is
determined: a parameterised correlated `EXISTS` over the library's tables, joined into
the host's hand-written query, PostgreSQL-specific (AUTHZ-GATE-003, LIB-API-004). The
expression renderer is not. Deciding whether a principal may act on one of the host's
records requires that record's ancestors, which live in the library's ancestry closure
and not on the host's row. An `Expression<Func<TResource, bool>>` whose parameter is the
host's entity therefore cannot decide the question from the host's row alone. It can do
so only by closing over a set the library materialised first, or by naming something the
host's provider can translate into a join against a library table.

*What the specification says.* AUTHZ-GATE-002 requires "an expression composable into
LINQ, and a parameterised SQL fragment for hand-written queries", both from one
definition, with every truth-table case asserted equal across the two (AC2). D-017 pairs
the two renderings with the two data-access tools of OPS-DATA-001: an
`Expression<Func<T, bool>>` "for LINQ composition", the fragment for hand-written
queries, and it makes the ancestry closure public contract "since hand-written SQL will
query it". LIB-HOST-002 requires the host to apply library-produced filters to its own
queries, requires the library to query no host table, and requires filters to "compose
into a host query without materialising rows". AUTHZ-PRIN-002 requires the filtering to
be part of the host's `WHERE` clause, with the row count the database returns equal to
the rows displayed. LIB-PKG-002 requires core contracts to compile without a database
dependency and neither rendering to be a bolt-on to the other. CONV-DESIGN-003 forbids
an `IQueryable` crossing a port and any EF Core type in an area project. No chapter says
what the expression's parameter is, nor how a host's query reaches the ancestry closure.

*The readings.*

1. **The expression is a predicate over the host's entity, and the host's own model maps
   the library's ancestry and grant tables**, which the public database schema and public
   ancestry closure of LIB-API-001 already permit it to know. The expression then carries
   a correlated subquery the host's provider translates. This satisfies AUTHZ-PRIN-002 at
   any volume and makes the two renderings genuinely parallel, at the cost of requiring
   every host that wants LINQ composition to map library tables into its own context,
   which no chapter asks of a host and which chapter 08 names no pattern for.
2. **The expression is a predicate over the host's entity whose body closes over the
   permitted identifiers**, the library having resolved them from its own tables first.
   Nothing is asked of the host and the filter is still part of the `WHERE` clause, so
   AUTHZ-PRIN-002 AC1 and AC2 hold as written. But a grant on a container permits every
   record beneath it, so the closed-over set is unbounded, which is the cost
   AUTHZ-PRIN-002 exists to avoid.
3. **The expression is the library's own evaluation path and the fragment is the host's.**
   The expression decides the single check of AUTHZ-PRIN-001 over the grant rows and the
   ancestry the library reads through its ports; the fragment is what a host composes
   into a list query, by either tool. One definition, two renderings, agreement asserted
   by the truth table. This asks nothing of the host and needs no new pattern, but it
   reads "composable into LINQ" as composable into the library's own LINQ rather than the
   host's, which D-017's pairing of the two renderings with the host's two data-access
   tools argues against.

*The smallest fix for each.* For reading 1: a sentence in `03` stating that a host
composing the filter into LINQ maps the library's ancestry and grant tables into its own
model, and a decision-log entry making that arrangement public contract beside the
closure table. For reading 2: a sentence in `03` stating the bound, an unbounded
identifier set contradicting AUTHZ-PRIN-002. For reading 3: a sentence in `03` stating
that the expression rendering serves the single check and the fragment serves the list
filter, which also settles what AUTHZ-GATE-002 AC2 compares.

The question is not resolved in code because it fixes a surface LIB-API-001 names as
public contract, because every remaining item of the phase routes through the gate it
decides, and because two of the three readings need a pattern chapter 08 does not name.
Nothing of the gate, the ancestry closure or the storage behind them is built ahead of
the answer.

**A second matter, not a question.** An instruction file under `docs/guide/` arrived with the D-158
update. It is excluded from the repository through `.git/info/exclude`, by the bare
pattern for its name already there, and so is not committed. the working guide's section 5
requires that the instruction files never enter history and names no exception for a
copy under `docs/`, so it was left excluded rather than committed. Say so if it was
meant to be part of the published specification.

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests.

The full gate is not run. The plan runs it once, at the end of the phase, and the phase
is not complete.

Tests: 632 discovered, 508 run locally and all passing. `tests/Janus.Core.Tests` 364,
`tests/Janus.Identity.Tests` 62, `tests/Janus.Authorization.Tests` 54,
`tests/Janus.Analyzers.Tests` 15, `tests/Janus.Privacy.Tests` 13;
`tests/Janus.Storage.Tests` 124, which need a container and run in the pipeline.
