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
    three new tables; the endpoint and status contracts name the new routes and codes;
    `SchemaContractTests.LIB_API_001_AC2_TheLibraryOwnedSchemaIsTheContractAsync` names
    the three tables, the two views and `preauthentication_sessions.signon_check`
    (a003d68f).
- `CHANGELOG.md`: four lines under Unreleased, `Added`.

**`07` LIB-TEST-004: the sample host (D-278), 8f6be068, 9de1bcc5**

- `workshop-manager` includes `technician`, leaving out `repair:read` and `fitting:read`,
  so the first administrator, who holds it, opens workshops through it and reads no
  repair; `repair:release` is declared `.NotInherited()`.

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
c9d0c32c**

- `GET /auth/signon?check=true`: the start pushes with `prompt=none`, the attempt keeps
  that it only checks (`signon_check`, migration `AddSignOnChecks`), and at the return
  `login_required` sends the browser to the stored return address with no session and
  nothing added; every other answer is as before. `check` other than `true` is 400
  naming `check`.
- `session.hint.domain` is in the catalogue as `10` section 4.1 gives it.
- Tests: `SignOnTests.BFF_SESS_006_ACheckWithNoAuthSessionReturnsAGuestAsAGuestAsync`,
  `BFF_SESS_006_ACheckWithAnAuthSessionReturnsThePersonSignedInAsync`,
  `BFF_SESS_006_ACheckOtherThanTrueIsRefusedAsync`;
  `PreAuthenticationStoreTests` keeps the check with the attempt; the sample host's
  `SignOnTests.LIB_TEST_004_AC2_AGuestsCheckOnlySignOnReturnsThemAsAGuestAsync` starts a
  check-only sign-on at its second application.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**`17` BFF-SESS-006, `09` `GET /auth/signon`, `10` section 4.1 (D-279 as amended, the
signed-in hint), 43d6a6e6**

- Where `session.hint.domain` names a domain, the cookie `identity-signed-in` (value `1`,
  `Domain` the key's value, `Secure`, `HttpOnly`, `SameSite=Lax`, expiring with the auth
  session) is written wherever an auth session is created: a sign-in, a provider sign-in,
  a registration's completion and a break-glass presentation. A sign-out removes it, and
  so does a silent authorization request that finds no auth session. A check-only start
  that finds the key set and no hint returns the browser to `returnTo` at once, with no
  push. Without the key nothing is written and every check asks.
- Tests: `SignOnTests.BFF_SESS_006_ACheckWithTheHintKeySetAndNoHintAnswersAtOnceAsync`,
  `BFF_SESS_006_ACheckCarryingTheHintReturnsThePersonSignedInAsync`,
  `BFF_SESS_006_ACheckFindingNoAuthSessionRemovesAHintLeftBehindAsync`,
  `BFF_SESS_006_AnAuthSessionCarriesTheHintUntilItsSignOutAsync` (every attribute),
  `BFF_SESS_006_WithoutTheHintKeyNoHintIsWrittenAsync`.
- `CHANGELOG.md`: the D-279 line brought up to date.

**`03` AUTHZ-GRANT-006, `09` `POST /admin/roles`, `10` section 1.3 (D-278 as amended,
question 3), a922d813**

- `POST /admin/roles` refuses turning `grantsMustEnd` off for a role that includes a role
  whose grants must end (409 `authz.role.inclusionmustend`), judged again under the locks.
- Tests: `RoleInclusionEndpointTests.AUTHZ_GRANT_006_AC6_TurningMustEndOffOverAnIncludedMustEndRoleIsRefusedAsync`.
- `CHANGELOG.md`: the inclusion line brought up to date.

**`04` PRIV-ROPA-001, `03` AUTHZ-GRANT-006 (D-278 as amended, question 5), dacc30c9**

- `IRegisterRoles.AllowedAsync` reports each role's effective permissions, through its
  inclusions, as the view every decision reads gives them.
- Tests: on PostgreSQL,
  `ComplianceStoreTests.PRIV_ROPA_001_ARoleIsReportedWithItsEffectivePermissionsAsync`.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**`04` PRIV-CONS-006a, `17` BFF-SESS-002 (D-279 as amended 2026-11-08, question 6),
425bcf3d**

- `identity-signed-in` is the library's sixth cookie name: a host declaring a cookie of
  that name fails startup, whether or not the key is set.
- `DeploymentCookies` lists it while the domain the start read is one (8446adb3 below):
  strictly necessary, no third party, at `session.default.absolute`. That is the longest
  an auth session lasts and the value the session cookie is listed at;
  `session.aal2.absolute` (ceiling 24 hours) and `breakglass.session.lifetime` (ceiling
  12 hours) are shorter at their defaults.
- A start with the key set over a notice version without the hint raises `degradation`
  under `notice.cookies`, naming it as added (the start's comparison, D-203).
- Tests: `DeploymentCookiesTests.PRIV_CONS_006a_TheSignedInHintIsListedWhileTheStartReadADomainAsync`;
  on PostgreSQL,
  `StartupValidationTests.PRIV_CONS_006a_AC4_TheHintKeySetOverANoticeWithoutTheHintRaisesADegradationAsync`
  and `PRIV_CONS_006a_AC3_ACookieDeclaredOutsideItsRuleIsRefusedAsync` with
  `identity-signed-in`; `PrivacyContractTests.PRIV_CONS_006a_TheLibrarySetsOnlyItsSixNecessaryCookies`
  (renamed from `...TheFiveNecessaryCookies`).
- `CHANGELOG.md`: the D-279 line brought up to date.

**`17` BFF-SESS-006, `06` OPS-CFG-004, `10` section 4.1 (D-279 as amended 2026-11-08,
question 7), 8446adb3**

- `session.hint.domain` is read once, at the start, before the start's notice
  comparison (`NoticeCookiesStartService`), into `SignedInHintDomain`; the hint and
  `DeploymentCookies` both answer to that value, so a `configure` takes effect at the next
  start, whose comparison raises a notice without the hint. A key that cannot be read at
  the start is a fault, as the mail server's endpoint is.
- Tests: on PostgreSQL,
  `StartupValidationTests.BFF_SESS_006_AConfigureOfTheHintKeyTakesEffectAtTheNextStartAsync`
  (a `configure` through `ProtectedConfiguration` changes neither the hint's domain nor the
  list of the running deployment and raises nothing; after a restart both follow and the
  comparison raises the hint as added);
  `SignOnTests.BFF_SESS_006_TheHintKeyTakesEffectAtTheNextStartAsync` (the key set since
  the start writes no hint and a check still pushes; after a start reads it, the sign-out
  removes the hint and a check without one answers at once). The sign-on tests that set
  the key read it as a start does.
- `CHANGELOG.md`: the D-279 line brought up to date.

**`03` AUTHZ-GRANT-006, AUTHZ-GRANT-005 (D-278 as amended 2026-11-08, the lead's
review), a9febbaf**

- `GrantService` judges the role a grant gives, and each role it offers as assignable, on
  the following graph (`RoleGraphs.ReadFollowingAsync`), as though inclusions were on
  whatever `authz.roleinclusion.enabled` reads; revoking a grant, judged as a grant is,
  follows. What the giver may hand out stays as the key reads (the gate's
  `role_effective_hands_out`).
- Tests: on PostgreSQL,
  `HandOutTests.AUTHZ_GRANT_006_AGrantIsJudgedAsThoughInclusionsWereOnAsync` (handing out
  by holding on, a giver holding an including role's own permission but not the included
  role's is refused the grant and not offered the role with the key off and on; a role
  carrying only what the giver holds is granted and offered). It fails with the grant
  judged as the key reads.
- `CHANGELOG.md`: the inclusion line names it.

## 2. Items not implemented

None.

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
9. The hint is written beside every auth session the authentication application creates,
   the break-glass session included, and removed by every silent request answered
   `login_required`, a session record that no longer answers included. `17` BFF-SESS-006
   governs: "written when an auth session is created", and "a check that finds no auth
   session removes a hint left behind".
10. `BrowserCookieTests.BFF_SESS_002_AC2_NoConfigurationKeyWeakensAnAttribute` and
    `BrowserProfileTests.AUTH_SESS_007_AC2_NoEndpointCanOptOut` list the files of the
    boundary that read a key; both now name `SignedInHint.cs`, and the first also holds
    that it reads `session.hint.domain` alone and fixes `Secure`, `HttpOnly` and
    `SameSite=Lax`. `17` BFF-SESS-006 (D-279) governs: the hint's `Domain` is the key's
    value, and no attribute of the session cookie becomes configurable.

## 4. Open questions

1. **D-279, the hint cookie's name (Tier 3; parked the hint).** `17` BFF-SESS-006 and the
   decision named the cookie `janus-signed-in`, against `08` CONV-NAME-001 (D-163).
   Answered by D-279's amendment (84749860): the cookie is `identity-signed-in`. Built.
2. **D-279, `10` section 4.8 (a document defect).** `session.hint.domain` was marked P in
   section 4.1 and missing from section 4.8, which failed two contract tests. Answered by
   D-279's amendment (84749860): the key is on section 4.8's list. The tests pass.
3. **D-278, turning `grantsMustEnd` off on an including role (Tier 2; parked it).**
   Answered by D-278's amendment (84749860): reading (a), refused with
   `authz.role.inclusionmustend`. Built.
4. **D-278, the reach of an inclusion change while `authz.roleinclusion.enabled` is off
   (Tier 2).** Answered by D-278's amendment (84749860): reading (a), judged as though the
   key were on, as built. Nothing changed.
5. **D-278, the records of processing (Tier 2).** Answered by D-278's amendment
   (84749860): reading (a), effective permissions. Built.
6. **D-279, the hint against the cookie roster and the privacy notice (Tier 3; parks the
   notice's listing of the hint).** `17` BFF-SESS-002's values name five cookies and say
   "The library sets no other cookie", and `04` PRIV-CONS-006a has the privacy notice list
   every cookie the deployment sets. `17` BFF-SESS-006 now has the authentication
   application set `identity-signed-in`. Answered by D-279's amendment of 2026-11-08
   (bc66f90f): the hint is the library's sixth cookie and the notice lists it while the
   key names a domain. Built.
7. **D-279 as amended 2026-11-08, comparing a change of `session.hint.domain` with the
   notice (Tier 2; parked that comparison).** The amendment compared "a change of
   `session.hint.domain`" with the notice "as a change of a cookie's lifetime key is",
   which `ConfigurationService` does inside `PUT /admin/config/{key}`. The key is
   protected, so that route refuses it with `configuration.key.protected` before any
   comparison, and putting it in the comparison set there would never run. It changes
   only through `configure` at the server (`ProtectedConfiguration`, in
   `Janus.Authentication`), which has neither the host's declared cookies nor the notice
   check (`Janus.Hosting`), and the CLI does not reference `Janus.Hosting`. Today the next
   start raises the difference, but the hint reads the key at each request, so between a
   `configure` and that start the hint can be set while the notice does not list it.
   Readings: (a) the hint reads the key at the start, as
   `integration.mailserver.endpoint` is read, so a change takes effect at the start whose
   comparison raises it (one read at startup held for the process); (b) `configure`
   compares the notice itself, which needs a port that answers the host's declared cookies
   outside the host; (c) leave it as built, raised at the next start.
   Also a document defect: `06` OPS-CFG-004 says its list and `10` section 4.8 are the same
   list (D-152), and `session.hint.domain` was on section 4.8 but not in `06`'s list.
   Answered by D-279's amendment for Q7 (9f5cb286): reading (a), and the key joins `06`'s
   list. Built.
8. **D-278 as amended 2026-11-08, the other hand-outs (Tier 2; nothing changed).** The
   amendment judges the role a grant gives, and the assignable list, as though
   inclusions were on. AUTHZ-GRANT-005 names other hand-outs that judge a role the same
   way and still read it as the key reads (`IRoleStore.EffectiveAsync`, the views): adding
   or removing a group's member (`GroupService`), an invitation's roles
   (`RoleCatalogue`), ending a membership (`MembershipRevocations`) and approving a
   recovery (`ApproverReach`). With the key off, each can hand out what switching it on
   would carry past the actor's reach. Readings: (a) judge each as a grant now is (one
   following read per change, or an `IRoleStore` read that ignores the key); (b) leave
   them as the key reads, the amendment binding a grant and the assignable list alone.

## 5. Gate result

- Fast checks on every commit: build with warnings as errors, analysers,
  `dotnet format --verify-no-changes`, the unit tests of the changed projects: green.
- Before the push: every unit and contract test, the commit-message gate and the
  full-history secret scan: green. On PostgreSQL, run here for the round's criteria:
  `RoleInclusionTests`, the truth table, the two seeding tests,
  `PreAuthenticationStoreTests`, `ComplianceStoreTests`, every test of
  `StartupValidationTests`, `HandOutTests`, `RoleInclusionTests`: green.
- The pipeline's pull-request run on 84749860 failed the schema contract
  (`LIB_API_001_AC2`) and two sample-host tests
  (`LIB_TEST_003_AC1_ACustomerTheFirstAdministratorAndAStaffMemberEachCallTheWorkshopsOwnEndpointsAsync`,
  `LIB_TEST_004_AJobReadsTheContactDetailsOfEveryoneWhoMayReadARepairAsync`), the
  manager reading repairs through its inclusion, and the one on 52708d18 the sample host
  again; fixed in 9de1bcc5 and a003d68f, and
  both pass here.
- The gate of record is the pipeline's run on this report's commit, named in the message
  to the lead.
