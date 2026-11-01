# Round H10: photos per organization, and a colleague's photo

## 1. Items implemented

- D-249, servicing (CONV-DEP-004 AC3): `.github/workflows/servicing.yml` holds
  `contents: read` beside `issues: write`, so it reads `global.json` in a private
  repository too. Read from the file; the workflow runs only on its schedule or a
  dispatch.
- D-249, an organization turns photos on (IDN-ATTR-002 AC6, OPS-CFG-002,
  AUTH-STEP-002a, `09` `PUT /admin/organizations/{id}/policy`, `10` section 4.1a):
  - an organization's `photos` `true` is no longer refused `config.policy.belowsystem`;
    its stated value is in force over the system policy's, and turning photos on stays a
    loosening, asking `system:administer` and the step-up; every other field is judged
    against the system policy as before;
  - members of that organization show photos, as AUTH-PRIN-002 resolves every field; an
    account of no organization follows the system policy;
  - `PolicyStrictnessTests.IDN_ATTR_002_AC6_AnOrganizationTurnsPhotosOnOverASystemPolicyThatHasThemOff`;
  - `OrganizationPolicyEndpointTests.IDN_ATTR_002_AC6_AnOrganizationTurnsPhotosOnAsALooseningAsync`;
  - `PhotoFlowTests.IDN_ATTR_002_AC6_AnOrganizationShowsPhotosWhereTheSystemPolicyDoesNotAsync`.
- D-250, a colleague's photo (IDN-ATTR-002 AC7, IDN-ATTR-003 AC4, OPS-ALERT-005 AC3,
  LIB-API-005, `09` `GET /organizations/{id}/members/{account}/photo`):
  - `IAccounts.MemberPhotoAsync` and the route answer the photo of a current member of
    the organization to another current member of it, asking no permission; the photo
    is shown as IDN-ATTR-002 shows it to its own account, where every organization the
    colleague belongs to enables photos;
  - every other case (the caller or the colleague no current member, an organization
    the deployment does not hold, photos off, a second organization of the colleague
    keeping them off, no photo set) answers 404 `identity.photo.notfound` alike; a
    system principal is refused `authz.denied`;
  - served with `Cache-Control: no-store` and no entity tag; an admitted read counts one
    record toward the reader's read volume, a refused one nothing, and no audit row is
    written;
  - `AccountAdministrationTests.IDN_ATTR_002_AC7_AMemberReadsAColleaguesPhotoAsync`
    (with OPS-ALERT-005 AC3);
  - `AccountAdministrationTests.IDN_ATTR_002_AC7_AColleaguesPhotoIsConcealedAlikeWhereTheReadIsNotAdmittedAsync`;
  - `PhotoFlowTests.IDN_ATTR_003_AC4_AMemberReadsAColleaguesPhotoOverTheWireAsync`,
    which also compares the refusals' bodies byte for byte, save the correlation
    identifier every answer carries.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `src/Janus.Core/IAccounts.cs`: D-250 names the member parameter `AccountId member`;
  the library has no `AccountId`, and every operation of `IAccounts` names an account by
  its `SubjectId` (`ReadPhotoAsync`, LIB-API-005), as `07` LIB-API-005 states no type.
  Rule: the member is a `SubjectId`, as the account is everywhere else in `IAccounts`.
- `tests/Janus.Hosting.Tests/PublicSurfaceTests.cs`: CONV-DESIGN-002 AC3's list of
  operations whose gate step is asking whose account is asking gains
  `IAccounts.MemberPhotoAsync`, which no permission governs and which asks the caller's
  membership before any other call (D-250). Rule: test infrastructure, the least change
  that lets the operation exist under the gate test.

## 4. Open questions

1. Answered. The lead's message asked the run to write the round's chapters; AGENTS.md
   sections 6 and 9 keep `docs/spec/`, `docs/guide/` and `docs/decision-log.md` the
   lead's. The lead withdrew the instruction, and the chapters came as
   `round-h10-docs` (13b6d4dd), merged. Nothing was parked.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push, with main merged after round H9: 5262,
  green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
