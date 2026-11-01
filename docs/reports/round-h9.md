# Round H9: a business organization from the server

## 1. Items implemented

Items are D-248's, as the chapters read at e999673d, merged into this branch (`06`
OPS-BOOT-001 AC10 to AC12, `01` IDN-ORG-002 AC4). Chapter changes this branch first
carried (318dc612) were taken back in 6cd89c2c before the docs branch was merged.

- The command `organization` of `Janus.Cli` (OPS-BOOT-001 AC10 to AC12, IDN-ORG-002 AC4):
  - `identity organization --name <name> --policy <file> --reason <text>`, with
    `--multiplememberships true|false` optional; the arguments are judged as
    `bootstrap` judges its own; the file is read before the database is reached, and
    one that does not read or does not hold the policy object (a member it does not
    have included) is refused `api.request.malformed` naming `policy`, a value a field
    does not take the route's `config.value.notallowed` naming `policy`; one reader of
    the policy object's members, `PolicyObject`, now serves the route and the command;
  - in one transaction, under a lock on the name's comparison key: refused
    `authz.denied` where no administrative organization stands; refused
    `api.request.malformed` naming `name` where two or more organizations hold the
    name; otherwise the organization is created where none holds it (its policy key
    written as `POST /admin/organizations` writes it, `identity.organization.created`
    recorded under the principal `organization` with the reason, `OrganizationCreated`
    announced), the policy is judged by the policy route's value checks (one
    judgement, `PolicyReplacement`, now serves both; no step-up and no
    `system:administer` for a loosening) and written where it differs, with what it
    raised recorded (AUTH-FACT-017) and a weakened gate alerted (D-083), and
    `organization.multiplememberships` is written where given and different;
  - every value is written through the writer of the values a command sets and
    recorded as `ops.configuration.changed` under the system principal `organization`
    (operation `configuration`, reason `OPS-BOOT-001`) with the reason given; no
    grant and no membership is written;
  - exit 0 with the organization's identifier as the one line on standard output; a
    refusal exits 1 with one JSON line on standard error, nothing of it written;
  - `organization` joins the reserved principal names no host job takes (IDN-PRIN-001
    AC7).
- Tests:
  - `OrganizationFromServerTests.OPS_BOOT_001_AC10_AnOrganizationIsCreatedWithItsPolicyUnderTheCommandsPrincipalAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_RunAgainTheCommandWritesNothingAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_ANameTwoOrganizationsHoldIsRefusedAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_ADeploymentNotBootstrappedIsRefusedAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_AnEmptyReasonIsRefusedAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_APolicyThePolicyRouteRefusesIsRefusedWithItsCodeAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC12_ALooseningIsWrittenWithNoStepUpAndNoPermissionAsync`,
    `OrganizationFromServerTests.IDN_ORG_002_AC4_ThePolicyKeepsTheDomainLockInForceAsync`
    (unit);
  - `OrganizationArgumentTests.OPS_BOOT_001_TheOrganizationCommandReadsItsArgumentsAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_TheOrganizationCommandJudgesItsArgumentsAsBootstrapDoesAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_AC11_ACommandLineWithoutAReasonIsRefusedAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_AC11_APolicyFileThePolicyRouteWouldRefuseIsRefusedAsync`
    (unit);
  - `OrganizationTests.OPS_BOOT_001_AC10_TheCommandCreatesTheOrganizationFromTheServerAsync`,
    `OrganizationTests.IDN_ORG_002_AC4_ThePolicyIsStoredAsThePolicyRouteStoresItAsync`,
    `OrganizationTests.OPS_BOOT_001_AC11_RunAgainTheCommandWritesNothingAsync`,
    `OrganizationTests.OPS_BOOT_001_AC12_ALooseningIsWrittenWithNoStepUpAndNoPermissionAsync`,
    `OrganizationTests.OPS_BOOT_001_AC11_ANameTwoOrganizationsHoldIsRefusedAsync`,
    `OrganizationTests.OPS_BOOT_001_AC11_ADeploymentNotBootstrappedIsRefusedAsync`,
    `OrganizationTests.OPS_BOOT_001_AC11_ARefusedRunWritesNothingAsync`
    (integration, PostgreSQL);
  - `CommandContractTests.LIB_API_001_AC2_OrganizationRefusesToReadWithoutWhatItDeclaresRequiredAsync`
    and the committed `commands.txt` (contract);
  - the command added to `CommandTests.OPS_SEC_001_EveryCommandReportsARefusalAsOneLineOnStandardErrorAsync`,
    `BootstrapRefusalTests.OPS_SEC_001_AC4_EveryCommandRefusesAConnectionAbsentBlankOrNotAStringAsMalformedAsync`
    and `AuthorizationModelTests.IDN_PRIN_001_AC7_AJobDeclaredUnderAReservedNameFailsStartup`;
  - `TruthTableTests.AUTHZ_TEST_001_AC2_EveryJobCaseDecidesTheSameWayThroughBothPathsAsync`
    restates the job decision under the reserved names as D-248 leaves them, as D-225's
    did (CONV-VCS-004): the names changed, no permission decision.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `OrganizationFromServer`, `photos` `true`: refused `config.value.notallowed` with
  `details.requires` `imageCodec`, since a value whose feature needs a host declaration
  is refused while the declaration is absent (OPS-CFG-003) and the command reads none,
  as bootstrap does not (OPS-BOOT-001).
- `OrganizationCommand`, a value a field of the file does not take: the route's code,
  `config.value.notallowed` (OPS-BOOT-001, "with the same value checks and codes"),
  naming the argument `policy` where the route names the key `policy.<organization>`,
  as every other refusal of the file names it and no organization is known before the
  database is reached.
- `OrganizationFromServer`, `OrganizationCreated`: announced in the creating
  transaction, since the events tell a host of changes after its deployment exists and
  only bootstrap's organization raises none (D-219 item 2, D-218 item 10).
- `AuthorizationModel` reserved names: `organization` added, since no host job takes the
  name of a principal of `10` section 5.29 (IDN-PRIN-001 AC7, D-206).
- The command writes runtime keys through the writer of protected keys, as a command on
  the server does (OPS-CFG-008, "From the server"), and holds their rows through
  `IConfigurationWrites.HoldAsync`; the gate test
  `LibraryStructureTests.OPS_CFG_004_AC2_OnlyTheCommandLineWritesAProtectedKey` lists the
  new service beside bootstrap and `configure`.
- `OrganizationArguments`, a missing `--reason`: `config.change.reasonrequired` with no
  `details.key`, as `configure` gives it, since `details.key` names the setting where one
  is changed (`10` section 1.5) and the command changes several.

## 4. Open questions

None. Defects for the lead, outside D-248's "Propagated to":

- `08` CONV-LAYOUT-001, the `Janus.Cli` row lists the commands the tool carries and does
  not name `organization`.
- `10` section 1.5 `config.change.reasonrequired`, section 5.24 `identity.organization.created`
  and section 5b `OrganizationCreated` name `configure`, bootstrap and
  `POST /admin/organizations` and not the command.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: green; the round's integration tests and the
  truth table's job cases, run here against PostgreSQL, green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
