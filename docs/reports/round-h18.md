# Round H18: the questions a host asks in a person's request

## 1. Items implemented

**`03` AUTHZ-GATE-007 criterion 6, `07` LIB-HOST-001, `09` `GET /admin/access/admitted`,
`10` section 1 (D-276 and its amendment), 3c2eb544 and 5ca3c37f**

- `AuthorizationDeclarationBuilder.AdmitsWhoMay(permission, asked)` declares the pair. A
  permission may admit several, each its own call. The builder throws nothing for a
  pair given twice, so the model refuses it at startup as D-276 says.
- `AuthorizationDeclaration.AdmitsWhoMay`, an init property, empty by default, carries
  the pairs to the model (section 3; accepted in D-276's amendment).
- The model's startup check fails with `model.startup.declarationinvalid`,
  `details.declaration` `binding.<permission>` and `details.field` `admitsWhoMay` where
  either side is a library-owned permission or one the model does not declare, and
  where a pair is declared twice.
- `IAccessGate.WhoMayAsync`, for a person: `grant:read` in the organization the record
  belongs to is decided first, then each host permission declared to admit the
  permission asked, each as the single check decides it there, recording nothing. The
  first that admits answers.
- Where none admits (5ca3c37f, Q2): the first declared permission held but stopped only
  by its step-up gate answers that step-up, and one stopped only by a restriction
  answers `authz.restricted`, neither recorded. Otherwise `grant:read`'s check answers
  and its refusal is the one recorded. A permission bound to a purpose resting on
  consent is stopped by that consent as well, an organization-wide check naming no data
  subject, so it falls to `grant:read`'s recorded refusal, as the amendment's
  "otherwise" says. A job's path is unchanged.
- `GET /admin/access/admitted` calls the gate; its line in the endpoint contract file is
  unchanged.
- `IAccessGate.WhoMayAsync`'s documentation names the new admission.
- Tests:
  - `AuthorizationModelTests.AUTHZ_GATE_007_AC6_APermissionDeclaredToAdmitTheQuestionOfWhoMayBuilds`;
  - `AuthorizationModelTests.AUTHZ_GATE_007_AC6_ADeclarationAdmittingTheQuestionOfWhoMayOutsideItsRuleFailsStartup`,
    five cases: an undeclared permission on either side, a library-owned one on either
    side, a pair declared twice;
  - `TruthTableTests.AUTHZ_GATE_007_AC6_EveryAskerCaseIsAnsweredTheWayTheTableSaysAsync`
    over a new table of nine rows: admitted, asked of a record and of the organization;
    refused for another permission, under a deny on the permission held, and where it
    is held only in another organization, each refusal recorded once, naming
    `grant:read` in the organization asked of; a restricted holder answered
    `authz.restricted`, and a holder of `document:publish`, bound to a step-up gate no
    factor it holds meets, answered `auth.stepup.unavailable`, neither recorded; a
    declared job naming the permission admitted outside a request and refused while one
    is served. With the first gate change taken out, the two admitted rows fail;
  - `RefusalRecordTests.AUTHZ_GATE_007_AC6_TheQuestionADeclaredPermissionAdmitsIsAnsweredOverTheEndpointAsync`:
    over the mounted endpoint, 200 naming the reader for `document:read`, and 403
    `authz.denied` for `document:edit`, its body carrying the refusal record.
- The test host declares `document:report` and `document:publish` each admitting
  `document:read`.
- `CHANGELOG.md`: one line under Unreleased, `Added`, stating the amendment's answers.

**`03` AUTHZ-GRANT-003 criterion 9, `09` `GET /account/grants` (D-274 and its
amendment), 8f6bb74d**

- `IGrants.OwnAsync(AccessContext, OrganizationId, CancellationToken)` answers the live
  grants the account the context is effective for holds in its own name in the
  organization, oldest first, as `OwnGrant(Id, Kind, Role, On, Deny, ExpiresAt,
  GrantedAt)`: no granter and no reason. No permission is asked, a restricted account is
  answered, and nothing is recorded or counted. A context effective for no account (a
  principal) is refused `authz.denied`, recording nothing, as `AssignableAsync` refuses
  one.
- `IGrants.HeldAsync` is unchanged; its documentation says the caller's own account
  needs `grant:read` too.
- `OwnGrant` and `OwnAsync` are in `PublicAPI.Unshipped.txt`.
- `GET /account/grants?organization={id}` in the account's endpoint group, under the
  live session, answering `[ { id, kind, resourceType, resourceId, role, deny,
  expiresAt, grantedAt } ]`, and 400 `api.request.malformed` naming `organization`.
- The endpoint contract file (`tests/Janus.Hosting.Tests/endpoints.txt`) takes the
  route, and the list of routes that need a session names it.
- Tests:
  - `OwnGrantsTests.AUTHZ_GRANT_003_AC9_APersonReadsTheirOwnLiveGrantsAloneAsync`, on
    PostgreSQL: a person holding no `grant:read` is answered their grant on a record and
    their organization-wide deny, oldest first, and neither an expired nor a revoked
    grant, a group's grant they belong to, nor another account's; nothing is recorded;
    `HeldAsync` on their own account is refused and recorded;
  - `OwnGrantsTests.AUTHZ_GRANT_003_AC9_ARestrictedAccountIsAnsweredItsOwnAndAPrincipalNoneAsync`,
    on PostgreSQL: a restricted account is answered its own; an account holding none,
    and an organization the deployment does not hold, are answered none; a job's
    context is refused;
  - `GrantEndpointTests.AUTHZ_GRANT_003_AC9_ACallersOwnGrantsAreAnsweredWithoutWhoGrantedThemOrWhyAsync`:
    over HTTP, the two live grants with `OwnGrant`'s eight members and no other, none
    in another organization, 400 naming `organization` where it is absent, and 403 for
    the caller's own grants through `GET /admin/grants`;
  - `EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract` and
    `SessionRequirementTests.BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed`
    with the new route;
  - `PublicSurfaceTests.CONV_DESIGN_002_AC3_EveryOperationMeetsTheGateBeforeItReadsOrWrites`
    names `IGrants.OwnAsync` among the operations on the caller's own records, whose
    gate step is asking whose account is asking (3f554e87).
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**D-275**: changes no code, as the round's documents say. Nothing built.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `src/Janus.Core/AuthorizationDeclaration.cs`: D-276 adds the builder member, and the
  declaration the builder produces had nowhere to carry the pairs. AUTHZ-MODEL-001 and
  AUTHZ-MODEL-006 govern, with D-273 for the form. The declaration is what the builder
  produces and what the model is validated from, so it carries what the builder
  declares, as an init property, the form D-273 gives a public record that gains a
  member. Its two accessors are in `PublicAPI.Unshipped.txt` beside the builder member.
  Accepted in D-276's amendment (859b4787).

## 4. Open questions

**Q1 (Tier 2).** D-274's assumption does not hold. The subject export carries less of a
grant than `HeldGrant` does.

- `ExportSource.Conferred` writes no granter and no reason; `HeldGrant` carries both,
  and `09` `GET /account/grants` listed both in its body. All of D-274 was parked.
- Readings asked: A, the whole `HeldGrant`; B, the self-read leaving out the granter
  and the reason; C, the export gaining both.
- Answered: D-274's amendment (859b4787), reading B as an operation of its own,
  `IGrants.OwnAsync` answering `OwnGrant`. Built (section 1).

**Q2 (Tier 3).** What a holder of a declared permission is answered where a restriction
or a step-up gate stops that permission.

- D-276 said both that such a permission is judged as any check judges it, and that
  anything else is refused `authz.denied` and recorded. The first build refused it and
  recorded the refusal.
- Answered: D-276's amendment (859b4787). Stopped only by its step-up gate, the
  step-up is answered; only by a restriction, `authz.restricted`; neither recorded;
  otherwise `grant:read`'s recorded refusal. Built (section 1).

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5223 in eleven projects), every
  contract test passes (214), and
  the commit-message, changelog, release, truth-table change, acceptance-criterion test
  name and forbidden-marker gates pass over `origin/main..HEAD`. The full-history secret
  scan finds nothing. The truth table, `OwnGrantsTests`, `ReverseLookupTests`,
  `StartupValidationTests`, `ExportOperationsTests` and `RefusalRecordTests` were run
  here against PostgreSQL.
- The pipeline's runs on 5ed1a337, before the answers, were green. On 67898e24 the
  contract tests failed: `CONV_DESIGN_002_AC3` found `IGrants.OwnAsync` reading grants
  before any gate, as an operation on the caller's own records does and the test had
  not been told. Fixed in 3f554e87.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
