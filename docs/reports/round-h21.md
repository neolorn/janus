# Round H21: the host's check of a removal of access; the roles read by whoever gives them

## 1. Items implemented

**`03` AUTHZ-GRANT-005 (the host's check), `07` section 1 and LIB-HOST-001, `09`
`DELETE /admin/grants/{id}`, `DELETE /admin/organizations/{id}/memberships/{subject}`,
`PUT .../end`, `10` sections 1 and 5.24 (D-281 as amended by the answers of afe61243),
02c08b48, 7987d422**

- `Janus.Core`: `IAccessRemovalCheck.CheckAsync(AccessRemoval, CancellationToken)`
  answering `Result`; `AccessRemoval` (kind, organization, subject, grant and role where
  one, group where one, the caller's `AccessContext`, the reason, the path, the grants
  and groups a membership's end reaches); `RemovedGrant`; `AccessRemovalKind`
  (`grantRevoked` · `membershipEnded` · `groupMemberRemoved`); `AccessRemovalPath`
  (`administration` · `library` · `scheduledEnd`); `ErrorCodes.RemovalUncheckable`
  (`authz.removal.uncheckable`); `AuditActions.AccessRemovalRefused`
  (`authz.access.removalrefused`).
- `AccessRemovalGuard` (authorization area) asks the check where the host registered
  one, and nothing is held or asked where it registered none:
  - every organization the change touches is held by a transaction-scoped advisory lock
    (`RemovalLocks`, `identity.access_removals/<organization>`), in the ordinal order of
    their identifiers, before the check is asked (question 6). A revocation takes it
    after the gate's second ask and before the role's and the grant's row locks, a
    group's member removal before the organization's groups are held, a membership's end
    after the membership's row lock and before its revocations, so no two removals take
    them the other way round;
  - a refusal is recorded in its own scope and unit of work as
    `authz.access.removalrefused` (security, under the change's organization, about the
    account where the subject is one, `details.kind`, `details.code`, `details.path`,
    `details.outcome` `refused`, the person or the job's principal as actor), and the
    change rolls back and writes nothing;
  - a check that throws, or does not answer before the request or the pass is abandoned
    for another reason, refuses with `authz.removal.uncheckable`, recorded alike; the
    change's own cancellation abandons it. No time limit (question 4). The catch is in
    the hosting project (`HostRemovalCheck`), as the authorization area holds none
    (AUTHZ-PRIN-003);
  - a code under a first segment the catalogue uses (`api`, `auth`, `authz`, `config`,
    `identity`, `integration`, `model`, `privacy`, `system`) or `session` is thrown as a
    fault, which the boundary answers 500 `system.fault` and logs (question 2).
- Paths checked: `IGrants.RevokeAsync` and its route (allow grants only, after the
  admission, the reach and the step-up); `IGroups.RemoveMemberAsync` and its route where
  the group, or one above it, holds a live allow grant, listing those grants; ending a
  membership now and setting an end in advance (`IInvitations`, both routes; a
  cancellation removes nothing), one call naming the membership with the allow grants it
  revokes and the groups it leaves; the job `membership-ends` when it carries an end
  out (path `scheduledEnd`, the job's principal as context); a member bringing their own
  end forward (question 5).
- Not checked: a deny's revocation, a grant's expiry (no write), an erasure (its own
  revocations), `IResources.RemoveAsync`, a derivation's writes, a group removal reaching
  only deny grants (question 5).
- A refused scheduled end rolls its transaction back: the membership stays current with
  its end set, the pass goes on with the next, and the next pass asks again.
- The path: the library marks its own route group (`RequestRemovalPaths.Mark`), so a
  removal through one of its routes is `administration` and anything else, a host's own
  endpoint or a direct call, is `library` (question 3).
- The boundary answers the host's code 409 with its details where the check refused
  with it in the request's scope (`RemovalRefusals`), so a code the reference does not
  list stays a fault everywhere else (BFF-HOST-003); `authz.removal.uncheckable` is 503
  in the status table and declared on the five routes.
- Startup: two checks registered fail with `model.startup.declarationinvalid`,
  `details.key` `accessRemovalCheck` (question 7).
- Tests:
  - on PostgreSQL, `AccessRemovalCheckTests` with a check keeping the last holder of a
    role (`company.lastowner`):
    `AUTHZ_GRANT_005_ARevocationLeavingTheRoleNoHolderIsRefusedAndWritesNothingAsync`,
    `AUTHZ_GRANT_005_TwoRevocationsAtOnceOfTheLastTwoHoldersAdmitOneAsync`,
    `AUTHZ_GRANT_005_ACheckThatThrowsRefusesAndWritesNothingAsync`,
    `AUTHZ_GRANT_005_AHostCodeUnderALibraryPrefixIsAFaultAsync`,
    `AUTHZ_GRANT_005_EndingAMembershipNowOrInAdvanceIsCheckedAsync`,
    `AUTHZ_GRANT_005_AScheduledEndTheCheckRefusesKeepsTheMembershipAsync`,
    `AUTHZ_GRANT_005_AnEndBroughtForwardIsCheckedAsync`,
    `AUTHZ_GRANT_005_AGroupMemberRemovalReachingAGrantIsCheckedAsync`,
    `AUTHZ_GRANT_005_ExpiryAndErasureAreNotCheckedAsync`;
  - on PostgreSQL, `StartupValidationTests.AUTHZ_GRANT_005_TwoAccessRemovalChecksAreRefusedAtStartupAsync`;
  - over HTTP, `AccessRemovalEndpointTests`:
    `AUTHZ_GRANT_005_ARevocationTheHostRefusesAnswersItsCodeAs409Async` (the host's
    code and details, path `administration`, the record),
    `AUTHZ_GRANT_005_ACheckThatThrowsRefusesWith503Async`,
    `AUTHZ_GRANT_005_AHostCodeUnderALibraryPrefixAnswersAFaultAsync` (`authz.` and
    `session.`), `AUTHZ_GRANT_005_ADenysRevocationIsNotCheckedAsync`;
  - `AccessRemovalGuardTests.AUTHZ_GRANT_005_TheLibrarysPrefixesAreTheFirstSegmentsOfItsCatalogue`;
  - the catalogues of codes, actions and statuses and the endpoint contract name the new
    code, action and 503s;
  - truth table (7987d422): a revocation of a role's last holder the host keeps
    (`Decided.KeptByHost`), and one leaving another holder (allowed).
- The fixture of the PostgreSQL tests registers a check that answers nothing for an
  organization no case armed, so every case there runs with the locks taken.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**`06` OPS-ALERT-001, `10` section 5.23 (D-281 as amended by 7a092f0d, question 8),
ef6e9872**

- `AlertCondition.AccessRemovalRefused` (`access-removal-refused`), High. A scheduled end
  the host's check refused, its transaction rolled back, raises it in a unit of work of
  its own, under the membership (the deduplication key's name), `details.code` the code
  the end was refused with and `details.membership` the membership; each pass that is
  refused again raises it again, the window's deduplication applying.
- Tests: on PostgreSQL,
  `AccessRemovalCheckTests.AUTHZ_GRANT_005_AScheduledEndTheCheckRefusesKeepsTheMembershipAsync`
  now reads the raised alert naming `company.lastowner` and the membership; the alert
  condition and severity contracts (`AlertsTests`, `VocabularyContractTests`) name it.
- `CHANGELOG.md`: the D-281 line names the alert.

**`07` LIB-TEST-004: the sample host (D-281), 6df18d8f, 1291dc55**

- `LastManager` keeps every organization's last `workshop-manager`: a revocation or a
  membership's end reaching a manager's grant, where the gate's `WhoMayAsync`, asked as
  the person making the change, names nobody else holding `grant:manage` across the
  organization, is refused `workshop.lastmanager` (`workshop.managersunread` where that
  person may not ask). An end the job carries out was judged when it was set, and is not
  judged again by the sample. The first version read the contract view the workshop
  maps, which AUTHZ-SEAM-001's test refuses (1291dc55).
- Tests: `WorkshopEndpointTests.AUTHZ_GRANT_005_TheWorkshopKeepsAnOrganizationsLastManagerAsync`
  (revoking the first administrator's grant over `DELETE /admin/grants/{id}` answers 409
  `workshop.lastmanager`, and the grant stands).

**`03` AUTHZ-GRANT-006 (Reading), `09` `GET /admin/roles` (D-282), 1e2e6877, 7987d422**

- `IRoles.AllAsync` and the route admit, as one admission with `role:manage` and
  `roleinclusion:manage` in the administrative organization, `grant:manage` held in any
  organization the caller holds a live allow grant in, its own or a group's, each asked
  as a question that records nothing; only where none admits it is `role:manage` asked
  of the single check, which records the refusal. Every change keeps its own admission.
- Tests: `RoleInclusionEndpointTests.AUTHZ_GRANT_006_AGiverOfRolesInABranchReadsThemReadOnlyAsync`
  (the read with inclusions and sources; an inclusion and a role definition refused 403;
  without the permission, the read refused); on PostgreSQL,
  `RoleInclusionTests.AUTHZ_GRANT_006_AGiverOfRolesInAnyOrganizationReadsThemReadOnlyAsync`
  (a holder of `grant:manage` alone outside the administrative organization reads the
  inclusion and the effective permissions with their sources, an inclusion by them is
  refused, an account holding none of the three is refused); truth table: the read by a
  caller managing grants outside the administrative organization (allowed, every role
  with what it includes and carries) and by a caller managing none (denied).
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**The release commit, fbd7b336 `chore: release 1.0.0-rc.11`**

- `CHANGELOG.md`: Unreleased becomes `## [1.0.0-rc.11] - 2026-11-08`, holding the round's
  lines under `Added`, and Unreleased is left empty.
- The public surface: `Janus.Core`'s 61 unshipped lines move into its shipped file, each
  beside its neighbours; no other project holds an unshipped line.
- The two lists are not drawn again (CONV-VCS-005, D-262). The leaked-password list
  (`# 2026-11-04`) and the Public Suffix List at version 2026-10-31_14-17-28_UTC were
  both drawn within 90 days before the release commit.
- No migration note: a release candidate opens no major version.
- Criterion verified otherwise: the release gate over `origin/main..HEAD` says the notes
  of 1.0.0-rc.11, 1464 characters, are the release's body.

## 2. Items not implemented

None.

## 3. Resolved by rule

1. `IMemberships` in D-281 is `IInvitations`' three methods (`EndMembershipAsync`,
   `SetMembershipEndAsync`, `BringMembershipEndForwardAsync`), and the check's
   registration is optional as `IMailServer`'s is: confirmed by the lead's answer of
   afe61243.
2. The path is read from metadata on the library's route group, not from an internal
   overload the endpoints call. LIB-API-005's tests hold every library endpoint to the
   one contract operation it serves, and the answer to question 3 (a host's direct call
   is `library`) is met by the mark: a removal through a route the library mounts is
   `administration`, any other `library`. The member's own route (`POST
   /account/memberships/{organization}/end`) is one of the library's routes, so it is
   `administration`.
3. The library's prefixes are held as a list in `AccessRemovalGuard`, and a unit test
   holds it to the first segments of `ErrorCodes` with `session`. Question 2 says read
   from the catalogue; CONV-CODE-004 AC2 keeps reflection out of the shipped code.
4. A check that throws is recorded as a refusal with the code
   `authz.removal.uncheckable`. D-281 records "a refusal"; the code is what the change
   was refused with.
5. `PublicSurfaceTests.NotServiceContracts` names `IAccessRemovalCheck`, as it names every
   interface the host implements and the library calls; the test infrastructure's
   `EndpointAnswers` accepts a code the host's check refused with in the request, which no
   route can declare. Both are test infrastructure (section 3, Tier 1).

## 4. Open questions

1. to 7. Asked before the build and answered in afe61243 (D-281 amended; `03` GRANT-005
   updated): 1 (a) `company.lastowner`; 2 (b) the catalogue's first segments and
   `session.`; 3 (a) an internal overload, a direct call is `library`; 4 (a) no timeout,
   a throw refuses 503, cancellation abandons; 5 own end brought forward checked,
   `IResources.RemoveAsync` and a group removal reaching only deny grants not; 6 (a) the
   library's advisory lock per organization in ordinal order; 7 the shape as read, at
   most one check.
8. The alert's severity (Urgent, which `AlertSeverity` does not hold) and identifier
   (`access.removal.refused`, absent from `10` section 5.23): answered (a) in 7a092f0d,
   `access-removal-refused`, High. It parked the alert, built in ef6e9872.

The two defects reported in `09` (the group members route and a member's own end not
listing the check's 409 and 503) are fixed in 7a092f0d.

## 5. Gate result

- Fast checks: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` over the library's solution are clean; every unit
  and contract test passes; the new and touched PostgreSQL classes and the truth table
  (344 cases) were run here; the sample host was built from this commit's packages, its
  format verified and its new test run.
- Before the push: the commit-message, changelog, release, truth-table-change,
  acceptance-test-names and forbidden-markers gates over `origin/main..HEAD` pass; the
  full-history secret scan finds nothing.
- The gate of record is the pipeline's run on this report's commit.
