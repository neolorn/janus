# Round H20: a role includes other roles; a sign-on that only checks; a person's own roles

## 1. Items implemented

**`03` AUTHZ-GRANT-006 (new), AUTHZ-GRANT-004, AUTHZ-GRANT-005; `07` LIB-HOST-001 (the
Roles row); `09` `/admin/roles` and `PUT|DELETE /admin/roles/{name}/inclusions/{included}`;
`10` sections 1.3, 2.1, 4.5a and 5.24 (D-278), d4cc0519**

- Inclusions are stored apart from roles: `identity.role_inclusions` (role, included,
  enabled, left-out permissions, left-out `handsOut`), keyed to both roles so neither is
  removed while one stands. Migration `AddRoleInclusions` adds it,
  `seeded_role_inclusions` and `uninherited_permissions`.
- Effective permissions and `handsOut` are worked out at each request, at any depth:
  - the views `identity.role_effective_permissions` and `identity.role_effective_hands_out`
    (recursive, ending at a row already reached), which leave out `system:administer`,
    an action not inherited, what an inclusion leaves out, an inclusion switched off, and
    every inclusion while `authz.roleinclusion.enabled` reads other than `true`;
  - `identity.effective_grants` now joins the effective view, so the single check, both
    renderings of the filter, AUTHZ-GATE-007 and the capability pages follow inclusions,
    and the hand-out rendering reads the effective `handsOut`;
  - `RoleGraph` holds the same rule in memory for what the views cannot answer: the read
    with sources (own first, then the shortest path, then the ordinal order of the names
    along it), the judgements of a change before it is made, and the explanation's
    `ExplainedGrant.IncludedThrough`.
- What a role carries is judged through its inclusions wherever it is granted, handed out,
  given through a group or an invitation, revoked from a member, judged at a recovery's
  approval, conferred by a derivation, or listed as assignable.
- `AuthorizationDeclarationBuilder.Includes(role, leftOutPermissions, leftOutHandsOut)`
  and `.NotInherited()`; the model refuses at startup a declared inclusion naming a role
  neither declared nor seeded at bootstrap, declared twice, leaving out a permission not
  declared or a member that is no role name, including a role whose grants must end in
  one whose grants need not end, or closing a loop.
- At each start the actions declared not inherited are written to
  `uninherited_permissions`, and each declared inclusion not yet seeded is made under the
  principal `role-declaration`, recorded `authz.role.included` and announced
  `RoleChanged` (`changed`).
- `IRoles.IncludeAsync`, `IRoles.RemoveInclusionAsync` and the routes: under
  `roleinclusion:manage` in the administrative organization, a reason, the `grant:manage`
  step-up, the caller's reach judged on what the change adds to the including role's
  effective permissions and `handsOut` or takes from them, audit
  `authz.role.included`/`authz.role.inclusionremoved` with the inclusion before and after,
  `RoleChanged` (`changed`) for the including role; refusals 404 `authz.role.notfound`,
  409 `authz.role.inclusionloop` and `authz.role.inclusionmustend`, 400 naming `leftOut`
  or `reason`. Every change holds the inclusions under one lock before the roles' rows,
  as definitions and removals of roles now do too.
- `POST /admin/roles` refuses turning `grantsMustEnd` on for a role an open-ended role
  includes (409 `authz.role.inclusionmustend`); `DELETE /admin/roles/{name}` refuses a
  role an inclusion names on either side (`authz.role.inuse`).
- `GET /admin/roles` answers `includes` and `effective` with `from`, and is open to
  `roleinclusion:manage` as to `role:manage`, as one admission.
- `roleinclusion:manage` is a library permission, so the bootstrap `system-administrator`
  role holds it; the migration gives it to an existing deployment's.
- Tests:
  - `RoleGraphTests`: `AUTHZ_GRANT_006_AC1_ARoleCarriesWhatItIncludesTwoLevelsDown`,
    `AUTHZ_GRANT_006_AC2_AnActionNotInheritedStaysWithTheRoleNamingIt`,
    `AUTHZ_GRANT_006_AC3_WhatAnInclusionLeavesOutIsNotPassedOn`,
    `AUTHZ_GRANT_006_AC5_TheKeyAndAnInclusionsSwitchEachTurnInclusionsOff`,
    `AUTHZ_GRANT_006_AC8_SystemAdministrationIsNeverPassedOn`,
    `AUTHZ_GRANT_006_AC9_EachEntryIsListedOnceWithItsSource`,
    `AUTHZ_GRANT_006_AC6_ARoleReachesWhatItIncludesAtAnyDepthOnOrOff`;
  - `AuthorizationModelTests`:
    `AUTHZ_GRANT_006_ADeclaredRoleKeepsItsInclusionsAndTheActionsNotInherited`,
    `AUTHZ_GRANT_006_AC10_ADeclaredInclusionThatCannotStandFailsStartup` (four cases),
    `AUTHZ_GRANT_006_AC10_ADeclaredLoopFailsStartup`;
  - `RoleInclusionEndpointTests` (over HTTP):
    `AUTHZ_GRANT_006_AC7_AnInclusionIsMadeSetAndReadBackWithItsSourcesAsync`,
    `AUTHZ_GRANT_006_AC7_AnInclusionIsRemovedAndARoleItNamesStaysAsync`,
    `AUTHZ_GRANT_006_AC6_LoopsAndMustEndBreachesAreRefusedAndWriteNothingAsync`,
    `AUTHZ_GRANT_006_AC7_ARefusedChangeWritesNothingAsync`,
    `AUTHZ_GRANT_006_AC9_TheRolesAreReadUnderEitherPermissionAsync`;
  - on PostgreSQL, `RoleInclusionTests`:
    `AUTHZ_GRANT_006_AC4_AChangeToTheLowestRoleReachesTheHoldersAtTheNextRequestAsync`
    (two levels deep, the lowest role's permission taken out refuses at the next request,
    nothing written above it),
    `AUTHZ_GRANT_006_AC5_TheKeyAndAnInclusionsSwitchEachTurnInclusionsOffAsync`,
    `AUTHZ_GRANT_006_AC6_LoopsAndMustEndBreachesAreRefusedAndWriteNothingAsync` (through
    `IRoles.IncludeAsync`), `AUTHZ_GRANT_006_AC9_TheReadAgreesWithTheViewEveryDecisionReadsAsync`;
  - on PostgreSQL, `StartupValidationTests.AUTHZ_GRANT_006_AC10_ADeclaredInclusionIsSeededOnceAsync`;
  - truth table: seven rows (two levels down, an inclusion switched off, a permission left
    out, a duty not inherited through an inclusion and named directly, a deny of the
    included role, a deny of the including role), each through the check, both renderings,
    the materialised run and AUTHZ-GATE-007's naming of the account (344 cases pass on
    PostgreSQL);
  - `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames` names the
    three new tables; the endpoint and status contracts name the new routes and codes.
- `CHANGELOG.md`: four lines under Unreleased, `Added`.

**`07` LIB-TEST-004: the sample host (D-278), 8f6be068**

- `workshop-manager` includes `technician`, and `repair:release` is declared
  `.NotInherited()`, so a manager does not release a kite through the inclusion.

**`03` AUTHZ-GRANT-006 (Reading) and `09` `GET /account/roles` (D-280), 0ec3d5d1**

- `HeldRole` and `IRoles.OwnAsync`: the roles of the live allow grants in the account's
  own name in the organization, each once, oldest grant first, with `effective` as
  `/admin/roles` gives it and `givenBy`, the roles whose effective `handsOut` names it,
  through inclusions too. No permission, nothing recorded; a principal's context is
  refused `authz.denied`. `GET /account/roles?organization={id}` in the account's group.
- Tests: `RoleInclusionEndpointTests.AUTHZ_GRANT_006_APersonReadsTheirOwnRolesWithWhatTheyCarryAsync`
  (a role held through its inclusion's parent carries the included role's permission with
  its source, `givenBy` through an inclusion, a deny and another organization's grant
  not listed, an absent organization malformed); the route is in the endpoint contract,
  the session list and the own operations of `CONV_DESIGN_002_AC3`.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**`17` BFF-SESS-006, `02` AUTH-SESS-012, `09` `GET /auth/signon` (D-279, the check only),
551cafe5**

- `GET /auth/signon?check=true`: the start pushes with `prompt=none`, the attempt keeps
  that it only checks (`signon_check`, migration `AddSignOnChecks`), and at the return
  `login_required` sends the browser to the stored return address with no session and
  nothing added; every other answer is as before. `check` other than `true` is 400
  naming `check`.
- `session.hint.domain` is in the catalogue as `10` section 4.1 gives it; nothing reads
  it yet (section 4, question 1).
- Tests: `SignOnTests.BFF_SESS_006_ACheckWithNoAuthSessionReturnsAGuestAsAGuestAsync`,
  `BFF_SESS_006_ACheckWithAnAuthSessionReturnsThePersonSignedInAsync`,
  `BFF_SESS_006_ACheckOtherThanTrueIsRefusedAsync`;
  `PreAuthenticationStoreTests` keeps the check with the attempt; the sample host's
  `SignOnTests.LIB_TEST_004_AC2_AGuestsCheckOnlySignOnReturnsThemAsAGuestAsync` starts a
  check-only sign-on at its second application.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

## 2. Items not implemented

- D-279's signed-in hint: writing `janus-signed-in` when an auth session is created,
  removing it at sign-out and where a silent request finds no auth session, and a
  check-only start answering at once without it. Parked on question 1. The round's
  criterion "with the hint key set and no hint answers without a push" is not met.
- Turning `grantsMustEnd` off on a role that includes a role whose grants must end:
  parked on question 3; nothing refuses it.

## 3. Resolved by rule

1. `RoleService.InclusionRefusedAsync`: a change to the inclusions of a role that carries
   `system:administer` asks `system:administer`. OPS-CFG-007 governs: "A change to a role
   that carries `system:administer` before or after it SHALL require `system:administer`",
   and a change to a role's inclusions is a change to it.
2. Must-end is judged on the inclusion being made, the including role and the included
   one. AUTHZ-GRANT-006 governs: every path from an open-ended role to a must-end one has
   a first edge of that kind, so refusing each such edge refuses every such path.
3. A loop is judged over every inclusion, on or off. AUTHZ-GRANT-006 governs: an
   inclusion switched off is still an inclusion, and switching it on would close the loop
   without a change to the graph.
4. `DeclaredRoleSeed`: a declared inclusion whose roles no longer both stand, that already
   stands, or that would close a loop or include a must-end role in an open-ended one as
   the roles now stand, is not made, and its pair is kept as seeded either way.
   AUTHZ-GRANT-004 governs seeding ("left as it stands where one does; either way the name
   is kept as seeded"), and AUTHZ-GRANT-006 seeds inclusions "under the start step and
   principal that seed declared roles".
5. `authz.role.included` and `authz.role.inclusionremoved` write each side as
   `{ enabled, leftOut: { permissions, handsOut } }`. `10` section 5.24 names `before` and
   `after`; `09` gives the inclusion this shape.
6. The read of `/admin/roles` under either permission asks the organization-wide
   capability of both, recording nothing, and where neither admits asks `role:manage`, whose
   refusal is the one recorded. `09` governs ("also open to `roleinclusion:manage`"), as
   AUTHZ-CONCEAL-004 records one refusal per refused read.
7. Test infrastructure: `HostFixture.Started` runs the declared-role start step, as a
   started deployment does, so the actions declared not inherited are written; the test
   host declares `document:approve` `.NotInherited()`; `AccessGateInMemory.CapabilitiesAsync`
   answers an organization's capabilities from what a test granted;
   `RolesInMemory.EffectiveAsync` works through `RoleGraph`. No runtime code is touched.
8. The catalogue member of `session.hint.domain` is `Settings.SessionSignedInDomain`.
   AUTH-PASS-006 AC2 governs: no line of a public surface file names a hint
   (`PasswordProhibitionTests.AUTH_PASS_006_AC2_NoApiAcceptsASecurityQuestionOrAnswer`
   failed on `SessionHintDomain`); the key itself is spelled as `10` spells it, and the
   `configure` command takes it as `--session.hint.domain`.

## 4. Open questions

1. **D-279, the hint cookie's name (Tier 3; parks the hint).** `17` BFF-SESS-006 and the
   decision name the cookie `janus-signed-in`. `08` CONV-NAME-001 (D-163) says the product
   name appears in no cookie, and that where a prefix keeps the library's artefacts apart
   from a host's, the prefix is `identity`; its criterion 2 is a source scan that fails on
   the name (`ProductNameTests.CONV_NAME_001_AC2_...`, which failed on it).
2. **D-279, `10` section 4.8 (a document defect; the pipeline is red until it is
   settled).** Section 4.1 marks `session.hint.domain` P; section 4.8, the OPS-CFG-004 list,
   does not name it. `SettingsCatalogueTests.OPS_CFG_004_AKeyMarkedProtectedIsOnTheOneList`
   and `OPS_CFG_001_AC1_ARedeployScopedKeyIsOnTheOpsCfg004List` read both sections and fail
   on the documents alone. The smallest fix is the key's row in section 4.8.
3. **D-278, turning `grantsMustEnd` off on an including role (Tier 2; parks it).**
   AUTHZ-GRANT-006 refuses "a role whose grants must end ... included by a role whose
   grants need not end", which turning the includer's `grantsMustEnd` off produces; `09`
   and `10` name only an inclusion and turning `grantsMustEnd` on. Readings: (a) refuse it
   with `authz.role.inclusionmustend` (one condition in `EndingRefused`); (b) allow it, the
   rule binding the two changes named.
4. **D-278, the reach of an inclusion change while `authz.roleinclusion.enabled` is off
   (Tier 2; built the stricter reading).** Judged on the effective sets as the key makes
   them, a change made while the key is off adds and takes away nothing and so meets no
   reach; turning the key on then admits it. What is built judges the change as though
   the key were on, so a change made while it is off reaches no further than one made
   while it is on. Readings: (a) as built; (b) as the key reads, with a one-line change
   (`ReadAsync` in place of `ReadFollowingAsync` in `RoleService`).
5. **D-278, the records of processing (Tier 2; nothing changed).** PRIV-ROPA-001's
   `IRegisterRoles.AllowedAsync` reports what each role allows from its own permissions.
   A grant of an including role admits more. Readings: (a) report effective permissions
   (the register reads `RoleGraphs`); (b) keep each role's own.

## 5. Gate result

- Fast checks on every commit: build with warnings as errors, analysers,
  `dotnet format --verify-no-changes`, the unit tests of the changed projects: green.
- Before the push: every unit and contract test, save the two of question 2, which fail on
  the documents; the commit-message gate and the full-history secret scan: green. On
  PostgreSQL, run here for the round's criteria: `RoleInclusionTests`, the truth table, the
  two seeding tests, `PreAuthenticationStoreTests`: green.
- The gate of record is the pipeline's run on this report's commit, named in the message
  to the lead.
