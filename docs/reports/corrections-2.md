# Corrections 2: D-163

Status: complete, full gate green, no open question.

D-163 keeps the product name in namespaces, project and package identifiers and
`AddJanus`, and nowhere else. This branch renames every other type, member, table,
schema, role, collation, cookie, header, constant and test that carried it, and adds the
source scan CONV-NAME-001 AC2 asks for as a contract test. The schema and the roles are
in migrations applied to no production database, so the migrations are rewritten in
place, not extended by renames. The work is four commits: the public types, the database
objects, the names on the wire, and the scan with what it found after the first three.

## 1. Items implemented

| D-163 point or tier | Items | Tests |
|---|---|---|
| D-163, the types (ledger 164) | CONV-NAME-001, CONV-LAYOUT-002 (`MapAuthorizationTables`), CONV-DESIGN-007 (`AddStorageArea`) | the declared public API of `Janus.Core` and `Janus.Hosting` (CONV-SETUP-003), `BrowserProfileTests.BFF_OWN_001_AC1_MountingTakesNoSecurityRelevantConfiguration`, `TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync` |
| D-163, schema `identity` and collation `identity_ci` (ledger 165) | OPS-DB-001, OPS-DB-002, OPS-MIG-007 | `SchemaTests.OPS_DB_001_AC2_TheCaseInsensitiveCollationIgnoresCaseAsync`, `SchemaTests.OPS_DB_002_AC1_TheLibraryKeepsItsOwnMigrationHistoryAsync`, `SchemaTests.OPS_DB_002_AC1_TheCollationLivesInTheLibrarysSchemaAsync`, `SchemaTests.OPS_MIG_007_AC1_TheMigrationsApplyASecondTimeAsync`, the double-migration gate |
| D-163, roles `identity_migrate`, `identity_app`, `identity_maintenance` (ledger 165) | OPS-MIG-003, OPS-MIG-003a, PRIV-RET-002 | `DatabaseRoleTests.PRIV_RET_002_AC5_OnlyTheMaintenanceRoleExecutesTheDropAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC1_TheMaintenanceRoleAltersNoSchemaAsync`, `DatabaseRoleTests.OPS_MIG_003a_AC2_EachMaintenanceFunctionIsTheMigrationRolesOwnAsync` |
| Tier 2, the database names no chapter fixes (ledger 165) | REG-SESS-003, AUTHZ-GATE-002, LIB-HOST-002 | `RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`, `PermissionRuleTests.LIB_HOST_002_AC1_NoRenderingReadsATableTheHostOwns`, `PermissionRuleTests.LIB_HOST_002_AC1_OnlyWhatTheHostRunsNamesTheRelationItDeclared` |
| D-163, cookies `__Host-identity-*` and headers `X-Identity-Request`, `X-Identity-Csrf` (ledger 166) | BFF-CSRF-001, BFF-CSRF-003, BFF-SESS-002, PRIV-CONS-006a | `BrowserProfileTests.BFF_CSRF_003_AC1_TheTwoHeadersAreNamedAsTheFrontendWritesThem`, `PrivacyContractTests.PRIV_CONS_006a_AC3_TheLibrarySetsOnlyTheFiveNecessaryCookies`, `BrowserCookieTests.BFF_SESS_002_AC1_EveryIssueCarriesTheFourAttributes` |
| Tier 2, the names on the wire no chapter fixes (ledger 166) | BFF-SESS-006, AUTH-OIDC-003, AUTH-PASS-004 | `SignOnTests.BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync`, `OidcFlowTests.AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync` |
| D-163, the scan (ledger 167) | CONV-NAME-001 AC2 | `ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint` |

Ledger entries 10 and 139, which chose and applied the two header names, each carry one
line "Superseded by D-163" as to those names.

## 2. Points of D-163 not implemented

| Point | Reason | Where |
|---|---|---|
| DNS record `_identity-verify` with value `identity-domain-verification=` | The domain lock (REG-DOM-001) is not built yet; nothing in the code names the record | Phase 8 |
| `AddIdentityArea` and its siblings other than `AddStorageArea` | Only `Janus.Storage` has its registration method; CONV-DESIGN-007 AC1 and AC2 are phase 10's, as the phase 1 report records | Phase 10 |

## 3. Resolved by rule

None.

## 4. Decided in the owner's absence

Under D-161 as amended by D-162. Each entry is in
`docs/reports/decisions-pending-review.md` in the same words, numbered as it is there.
Nothing here touches who is checked, what is refused or what a signal does: every name
changed is the same artefact under a new name, and the `__Host-` prefix of every cookie
is kept.

| # | Decision |
|---|---|
| 164 | The public types are named for what they are |
| 165 | The database names no chapter fixes take the prefix only where they meet a host's |
| 166 | The names on the wire no chapter fixes take the prefix, as the ones D-163 fixes do |
| 167 | The scan reads the source, and allows the name only as the head of a dotted name |

## 5. Gate result

Fast checks on every commit, all green: build with warnings as errors, the analysers of
CONV-CODE-008, `dotnet format --verify-no-changes` over `Janus.slnx`, and the unit
tests. From fresh builds, the local counts at the end of this branch, unit and contract:
`Janus.Analyzers.Tests` 15, `Janus.Authentication.Tests` 557,
`Janus.Authorization.Tests` 114, `Janus.Core.Tests` 435, `Janus.Hosting.Tests` 262,
`Janus.Identity.Tests` 67, `Janus.Privacy.Tests` 148 and `Janus.Storage.Tests` 29, none
failing. The integration suites of `Janus.Storage.Tests` (252) and
`Janus.Hosting.Tests` (126) were also run locally against the rewritten migrations,
none failing.

Full gate: GitHub Actions runs `35880448335` (push) and `35880456578` (pull request) on
branch `corrections-2`, pull request #2, green on every job. The pipeline's counts:
`Unit tests` 1545, `Contract tests` 66, `Integration tests` 378 and `Truth-table suite`
49, none failing. `Integration tests`, `Double migration run`, `Destructive-operation
detection report`, `Truth-table suite` and `Dependency vulnerability alerting` run on the
pull-request event and `Secret scanning` on the push event, as CONV-GATE-002 states, so
the two runs together are one pass of the table of CONV-GATE-001.

The commit after the two runs above changes this section and the status line alone.
