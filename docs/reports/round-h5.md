# Round H5: the gender field, grants that must end, and what H4 left

## 1. Items implemented

Items are D-233 as D-238 amends it, D-235's log line, D-236, D-237 and D-238, as the
chapters read after the merge of `round-h5-docs` at 4d9dbf9c. Criteria proven by tests
that existed before the round, and still pass, are marked "at base".

- D-237, a role whose grants must end (AUTHZ-GRANT-003, AUTHZ-GRANT-004, LIB-HOST-001):
  - `GrantEndpointTests.AUTHZ_GRANT_003_AC7_AGrantOfARoleWhoseGrantsMustEndCarriesAnEndAsync`
    (an allow and a deny without `expiresAt` refused 422 `authz.grant.endrequired`,
    `details.role` naming the role, nothing written; with `expiresAt` written);
  - `MaterialisationTests.AUTHZ_GRANT_003_AC7_ARoleWhoseGrantsMustEndIsMaterialisedWithNoEndAsync`
    (integration; a materialised grant of such a role is written with no end);
  - `GrantEndpointTests.AUTHZ_GRANT_004_AC8_TheNextOpenGrantOfARoleTurnedToEndIsRefusedAsync`;
  - `RoleEndpointTests.AUTHZ_GRANT_004_AC8_ARoleCarriesWhetherItsGrantsMustEndAsync`
    (read and set over `/admin/roles`, `false` where absent, malformed naming the
    member where not a boolean, recorded before and after, an open-ended grant left
    standing);
  - `AuthorizationModelTests.AUTHZ_GRANT_004_AC8_ADeclaredRoleWhoseGrantsMustEndIsKeptSo`
    (`.GrantsMustEnd()` marks the role declared before it, and is refused with none);
  - `StartupValidationTests.AUTHZ_GRANT_004_AC7_ADeclaredRoleIsSeededOnceAndThenLeftToTheAdministratorsAsync`
    (integration; a declared role is seeded with `grants_must_end` and its seeding
    recorded with it);
  - `RoleStoreTests.AUTHZ_GRANT_004_WhetherARolesGrantsMustEndIsKeptWithItAsync` and
    `RoleAuditTests.AUTHZ_GRANT_004_AC4_ADefinitionRecordsWhatTheRoleWasAndBecameAsync`
    (integration; `authz.role.defined` carries `grantsMustEnd` before and after);
  - the truth table restates the expiry cases for a role whose grants must end:
    `a grant of a role whose grants must end, that has expired` and `... that expires later`.
  - Migration `20261101014629_AddRoleGrantsMustEnd`.
- D-236, the grammatical gender (IDN-ATTR-007, REG-PROF-001, IDN-READ-003,
  OPS-BOOT-001, `10` section 4.1a):
  - `AccountServiceTests.REG_PROF_001_AC4_AMemberSetsChangesAndClearsTheGrammaticalGenderAsync`
    (set, changed, left alone, cleared; each audited as a profile change, which holds
    no value);
  - `AccountServiceTests.REG_PROF_001_AC4_TheGrammaticalGenderIsRefusedWhereThePolicyDoesNotEnableItAsync`
    (422 `identity.profile.notaccepted`, read as nothing; another value malformed
    naming the member);
  - `AccountApplicationTests.REG_PROF_001_AC4_TheGrammaticalGenderOverTheEndpointAsync`
    (over HTTP: 422, 400 naming `grammaticalGender`, then set and read back);
  - `AccountReadTests.IDN_READ_003_AC1_TheGrammaticalGenderIsReadOnlyWhereThePolicyEnablesItAsync`
    (the admin and host read, on and off);
  - `PolicyResolutionTests.IDN_ATTR_007_TheGrammaticalGenderIsOnWhereEveryOrganizationEnablesItAsync`
    (off with no membership, on for a member, off for a member of one organization
    that turned it off);
  - `PolicyStrictnessTests.IDN_ATTR_007_TheGrammaticalGenderLoosensNothingEitherWay`;
  - `OrganizationServiceTests.IDN_ATTR_007_ACreatedOrganizationStartsWithTheGrammaticalGenderOnAsync`
    and `OrganizationEndpointTests.IDN_ORG_002_AnOrganizationIsCreatedAsync` (the
    override written as `{"grammaticalGender": true}`);
  - `BootstrapTests.OPS_BOOT_001_AC9_BootstrapWritesTheAdministrativeOrganizationsGrammaticalGenderOnAsync`
    (integration);
  - `FrontendSettingsTests.REG_IDENT_001_ABrowserCarryingNothingReadsTheKeysAndTheSystemPolicyAsync`
    (`GET /settings`'s `policy` carries it, `false` for the system policy);
  - `ExportSourceTests.IDN_ATTR_007_TheGrammaticalGenderIsExportedAndErasedWithTheProfileAsync`
    (integration);
  - `ModelTests.IDN_ATTR_007_AC1_TheProfileIsTheFiveFieldsAndNothingElse`;
  - REG-PROF-001 AC4, never asked at registration: the registration session takes no
    such field (at base; no registration route or session member was added).
  - Migration `20261101022322_AddProfileGrammaticalGender`.
- D-233 as D-238 amends it, the second handle (AUTH-FACT-003 AC7, AUTH-FACT-016,
  API-LAND-001):
  - `AuthenticationServiceTests.AUTH_FACT_003_AC7_APressThatLeavesTheCheckAnswersASecondHandleOnceAsync`
    (answered once, kept as its fingerprint, of the first handle's length; a second
    press answers none; the resend and the code take it; the first handle then answers
    as for an ended challenge);
  - `AuthenticationServiceTests.AUTH_FACT_003_AC7_CompletingThroughTheFirstHandleEndsTheSecondAsync`;
  - `SignInFlowTests.AUTH_FACT_003_AC7_TheLandingTabCompletesTheSignInWithTheSecondHandleAsync`
    (over HTTP, the landing tab completes the sign-in alone);
  - `ChallengeStoreTests.AUTH_FACT_003_AC7_TheSecondHandleIsHeldAsItsFingerprintAndFindsTheChallengeAsync`
    (integration; the column holds the fingerprint, the row is found and locked by
    either handle, and goes with the challenge).
  - Migration `20261101023745_AddChallengeLandingHandle`.
- D-235's log line (`10` section 5a, D-238):
  - `StartupValidationTests.AUTH_STEP_001_ADeploymentWhosePoliciesNameARetiredActionStartsAsync`
    (integration; one Information line for the system policy and one for the
    override, each naming the policy and `alerting:destinations`; a read after the
    start adds none);
  - `SettingWrittenFormTests.AUTH_STEP_001_TheRetiredActionsAStoredPolicyNamesAreFound`.

Criteria a test cannot decide, and how each was verified:

- AUTH-FACT-003 AC7, no handle logged in clear: the second handle is carried only by
  members marked `[NeverLogged]` (`LandedSignIn.Handle`, `SignInLanding.ChallengeId`,
  `SignInProgressView.ChallengeId`, the answer's parameter, CONV-LOG-003); verified by
  reading that none of them reaches a log call.
- D-235, nothing else logs the retired gate: the configuration store and the setting
  forms hold no logger, and the command-line tool's reads go through them; verified by
  reading.

## 2. Items not implemented

None. Two behaviours the decisions do not settle are asked in section 4; neither
changes what was built.

## 3. Resolved by rule

None.

## 4. Open questions

**Q21 (Tier 2), open.** An invitation naming a role whose grants must end.

- What the code does: an invitation's `roles` are granted across the organization when
  the membership attaches, with no end (REG-INV-001), whatever the role's
  `grantsMustEnd`.
- What the spec says: D-237 and AUTHZ-GRANT-003 bind `POST /admin/grants` and the
  library call; the invitation routes are not named.
- Reading A: an invitation may not name such a role (refused at issue). Smallest fix: a
  sentence in `09` `POST /admin/organizations/{id}/invitations`.
- Reading B: the invitation carries an end for such a role's grant. Smallest fix: a
  member and a sentence in `09`, and the chapter of REG-INV-001.
- Reading C: an invitation's grant stays open-ended. Smallest fix: a sentence in `03`.
- Parks: nothing built; an invitation behaves as before the round.

**Q22 (Tier 2), open.** Organizations created before this round.

- What the code does: a new organization's override is written
  `{"grammaticalGender": true}`, and the family's default is the same; an organization
  created before the round holds an override written without the field, so its members
  inherit the system policy's `false`.
- What the spec says: `10` section 4.1 and D-236, every organization starts `true`;
  nothing states whether a stored override is brought to it.
- Reading A: a migration sets the field `true` in every stored organization override
  that does not state it. Smallest fix: a sentence in `06` OPS-MIG-001.
- Reading B: stored overrides are left as written. Smallest fix: a sentence in `10`
  section 4.1.
- Parks: nothing built.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit and contract test passes (5198 in eleven
  projects); the commit-message, changelog, acceptance-test-name, forbidden-marker and
  truth-table gates and `release.sh` pass over the round's commits; the full-history
  secret scan finds nothing.
- Every integration test the round added runs first in the pipeline.
- Pipeline on 820673f0: the push run (41267925164) green; the pull-request run
  (41267928788) red in the integration job alone, on
  `ConfigurationStoreTests.ReadAsync_AMemberOfAFamily_ReadsBackAsItWasWrittenAsync`,
  which read an override the deployment never wrote as no override; the family's default
  now holds the grammatical gender on (D-236), and the test reads it as the default.
  Fixed in the commit before this report's update.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
