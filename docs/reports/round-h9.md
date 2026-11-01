# Round H9: a business organization from the server

## 1. Items implemented

Items are D-248's, built from the decision as committed at 10c44a32. The chapters and
the plan's H9 row come from the round's docs branch, not yet merged here: the tests
name the criteria D-248 states as OPS-BOOT-001 AC10 to AC13 (the command, its rerun,
the shared name, the refusals) and IDN-ORG-002 AC4 (the policy route's checks), and are
renamed to the docs branch's numbering where it differs once it is merged. Chapter
changes this branch first carried (318dc612) were taken back in 6cd89c2c.

- The command `organization` of `Janus.Cli` (D-248):
  - `identity organization --name <name> --policy <file> --reason <text>`, with
    `--multiplememberships true|false` optional; the arguments are judged as
    `bootstrap` judges its own, and the file is read before the database is reached as
    the policy route reads its policy object (one reader, `PolicyObject`, now serves
    both);
  - in one transaction, under a lock on the name's comparison key: refused
    `authz.denied` where no administrative organization stands; refused
    `api.request.malformed` naming `name` where two or more organizations hold the
    name; otherwise the organization is created where none holds it (its policy key
    written as `POST /admin/organizations` writes it, `identity.organization.created`
    recorded with the reason, `OrganizationCreated` announced), the policy is judged
    by the policy route's rule (one judgement, `PolicyReplacement`, now serves both)
    and written where it differs, with what it raised recorded (AUTH-FACT-017) and a
    weakened gate alerted (D-083), and `organization.multiplememberships` is written
    where given and different;
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
    `OrganizationFromServerTests.OPS_BOOT_001_AC11_AnOrganizationThatStandsIsChangedOnlyWhereTheFileDiffersAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC12_ANameTwoOrganizationsHoldIsRefusedAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC13_ADeploymentNotBootstrappedIsRefusedAsync`,
    `OrganizationFromServerTests.OPS_BOOT_001_AC13_AnEmptyReasonIsRefusedAsync`,
    `OrganizationFromServerTests.IDN_ORG_002_AC4_APolicyThePolicyRouteRefusesIsRefusedWithItsCodeAsync`,
    `OrganizationFromServerTests.IDN_ORG_002_AC4_ThePolicyKeepsTheDomainLockInForceAsync`
    (unit);
  - `OrganizationArgumentTests.OPS_BOOT_001_TheOrganizationCommandReadsItsArgumentsAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_TheOrganizationCommandJudgesItsArgumentsAsBootstrapDoesAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_AC13_ACommandLineWithoutAReasonIsRefusedAsync`,
    `OrganizationArgumentTests.OPS_BOOT_001_AC13_APolicyFileThePolicyRouteWouldRefuseIsRefusedAsync`
    (unit);
  - `OrganizationTests.OPS_BOOT_001_AC10_TheCommandCreatesTheOrganizationFromTheServerAsync`,
    `OrganizationTests.IDN_ORG_002_AC4_ThePolicyIsStoredAsThePolicyRouteStoresItAsync`,
    `OrganizationTests.OPS_BOOT_001_AC11_RunAgainTheCommandWritesOnlyWhatDiffersAsync`,
    `OrganizationTests.OPS_BOOT_001_AC12_ANameTwoOrganizationsHoldIsRefusedAsync`,
    `OrganizationTests.OPS_BOOT_001_AC13_ADeploymentNotBootstrappedIsRefusedAsync`,
    `OrganizationTests.OPS_BOOT_001_AC13_ARefusedRunWritesNothingAsync`
    (integration, PostgreSQL);
  - `CommandContractTests.LIB_API_001_AC2_OrganizationRefusesToReadWithoutWhatItDeclaresRequiredAsync`
    and the committed `commands.txt` (contract);
  - the command added to `CommandTests.OPS_SEC_001_EveryCommandReportsARefusalAsOneLineOnStandardErrorAsync`,
    `BootstrapRefusalTests.OPS_SEC_001_AC4_EveryCommandRefusesAConnectionAbsentBlankOrNotAStringAsMalformedAsync`
    and `AuthorizationModelTests.IDN_PRIN_001_AC7_AJobDeclaredUnderAReservedNameFailsStartup`.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `OrganizationFromServer`, a deployment not bootstrapped: D-248 states the refusal and
  not its code. Rule applied: before bootstrap marks an administrative organization,
  every organization lifecycle and policy operation is refused (AUTHZ-SCOPE-001), which
  the gate answers `authz.denied`, the code bootstrap gives the opposite state
  (OPS-BOOT-001 AC1).
- `OrganizationDirectory.HoldNamedAsync`, "of that name": the organization row keeps its
  name's comparison key (`canonical_name`), and a name is judged on its comparison key as
  every identifier is (IDN-ACCT-004, IDN-ACCT-005), so `Northwind Clinics` and
  `northwind clinics` are one name.
- `OrganizationFromServer`, `photos` `true`: refused `config.value.notallowed` with
  `details.requires` `imageCodec`, since a value whose feature needs a host declaration
  is refused while the declaration is absent (OPS-CFG-003) and the command reads none,
  as bootstrap does not (OPS-BOOT-001).
- `OrganizationCommand`, the policy file's refusals: the route's codes are kept (D-248);
  a file that cannot be read, is not JSON or holds no object, and a value the policy
  object does not take, name the argument `policy` where the route names the body or
  the key `policy.<organization>`, since a command's values are judged before the
  database is reached and its refusals name the argument (OPS-BOOT-001), and no
  organization is known then.
- `OrganizationFromServer`, `OrganizationCreated`: announced in the creating
  transaction, since the events tell a host of changes after its deployment exists and
  only bootstrap's organization raises none (D-219 item 2, D-218 item 10).
- `OrganizationFromServer.Principal`: D-248 names the principal `organization` and not
  its operation or reason. Rule applied: `configuration` is the operation of changing the
  deployment's configuration from the server (`10` section 5.28), and OPS-BOOT-001 is the
  item the command belongs to (D-248, "Propagated to"); the docs branch's `10` row
  governs where it says otherwise.
- `AuthorizationModel` reserved names: `organization` added, since no host job takes the
  name of a principal of `10` section 5.29 (IDN-PRIN-001 AC7, D-206).
- `ConfigurationAdministration`'s neighbours: the command writes runtime keys through the
  writer of protected keys, as a command on the server does (OPS-CFG-008, "From the
  server"), and holds their rows through `IConfigurationWrites.HoldAsync`; the gate test
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
- Unit and contract tests before the push: green; the round's integration tests, run
  here against PostgreSQL, green.
- The acceptance-criterion test-name gate fails until the docs branch is merged, since
  the criteria the tests name are not yet in the chapters.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
