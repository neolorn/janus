# Round H18: the questions a host asks in a person's request

## 1. Items implemented

**`03` AUTHZ-GATE-007 criterion 6, `07` LIB-HOST-001, `09` `GET /admin/access/admitted`,
`10` section 1 (D-276), 3c2eb544**

- `AuthorizationDeclarationBuilder.AdmitsWhoMay(permission, asked)` declares the pair. A
  permission may admit several, each its own call. The builder throws nothing for a
  pair given twice, so the model refuses it at startup as D-276 says.
- `AuthorizationDeclaration.AdmitsWhoMay`, an init property, empty by default, carries
  the pairs to the model (section 3).
- The model's startup check fails with `model.startup.declarationinvalid`,
  `details.declaration` `binding.<permission>` and `details.field` `admitsWhoMay` where
  either side is a library-owned permission or one the model does not declare, and
  where a pair is declared twice.
- `IAccessGate.WhoMayAsync`, for a person: `grant:read` in the organization the record
  belongs to is decided first, then each host permission declared to admit the
  permission asked, each as the single check decides it there, recording nothing. The
  first that admits answers. Where none does, `grant:read`'s check answers, so its
  refusal is the one recorded, as `AdmitAsync` and `ReadableAsync` record theirs. A
  declared permission held under a restriction or with a step-up gate unmet admits
  nothing, and the question is refused `authz.denied` and recorded (Q2). A job's path
  is unchanged.
- `GET /admin/access/admitted` calls the gate and needed no change; the endpoint
  contract file is unchanged, the route answering 200, 400 and 403 as before.
- `IAccessGate.WhoMayAsync`'s documentation names the new admission.
- Tests:
  - `AuthorizationModelTests.AUTHZ_GATE_007_AC6_APermissionDeclaredToAdmitTheQuestionOfWhoMayBuilds`;
  - `AuthorizationModelTests.AUTHZ_GATE_007_AC6_ADeclarationAdmittingTheQuestionOfWhoMayOutsideItsRuleFailsStartup`,
    five cases: an undeclared permission on either side, a library-owned one on either
    side, a pair declared twice;
  - `TruthTableTests.AUTHZ_GATE_007_AC6_EveryAskerCaseIsAnsweredTheWayTheTableSaysAsync`
    over a new table of seven rows: admitted, asked of a record and of the
    organization; refused for another permission, under a deny on the permission held,
    and where it is held only in another organization, each refusal recorded once,
    naming `grant:read` in the organization asked of; a declared job naming the
    permission admitted outside a request and refused while one is served. With the
    gate change taken out, the two admitted rows fail;
  - `RefusalRecordTests.AUTHZ_GATE_007_AC6_TheQuestionADeclaredPermissionAdmitsIsAnsweredOverTheEndpointAsync`:
    over the mounted endpoint, 200 naming the reader for `document:read`, and 403
    `authz.denied` for `document:edit`, its body carrying the refusal record.
- The test host declares `document:report` admitting `document:read`.
- `CHANGELOG.md`: one line under Unreleased, `Added`.

**D-275**: changes no code, as the round's documents say. Nothing built.

## 2. Items not implemented

**`03` AUTHZ-GRANT-003 criterion 9, `09` `GET /account/grants` (D-274)**: parked on Q1.
Nothing of it is built: not the self-read in `IGrants.HeldAsync`, the route, its line in
the endpoint contract file, or its `CHANGELOG.md` line.

## 3. Resolved by rule

- `src/Janus.Core/AuthorizationDeclaration.cs`: D-276 adds the builder member, and the
  declaration the builder produces had nowhere to carry the pairs. AUTHZ-MODEL-001 and
  AUTHZ-MODEL-006 govern, with D-273 for the form. The declaration is what the builder
  produces and what the model is validated from, so it carries what the builder
  declares, as an init property, the form D-273 gives a public record that gains a
  member. Its two accessors are in `PublicAPI.Unshipped.txt` beside the builder member.

## 4. Open questions

**Q1 (Tier 2).** D-274's assumption does not hold. The subject export carries less of a
grant than `HeldGrant` does.

- `ExportSource.Conferred` writes a grant's identifier, organization, role, deny, kind,
  `grantedAt`, resource type and identifier, `expiresAt` and `revokedAt`. It writes no
  granter and no reason. Its comment says why: who granted or revoked a grant, and why,
  is about the person who acted, so it does not cross.
- `HeldGrant` carries `GrantedBy` (a subject identifier) and `Reason`, both
  non-nullable. `09` `GET /account/grants` lists `grantedBy` and `reason` in its body.
- D-274 says that where the export carries less, this is a question and not a reason to
  answer more. All of D-274 is parked on it.
- Reading A: the self-read answers the whole `HeldGrant`, granter and reason included,
  as `09` writes the body. Smallest fix: none in code beyond D-274; the self-read then
  discloses to the holder what the export holds back.
- Reading B: the self-read leaves out the granter and the reason. Smallest fix: a
  public surface change, either a record of its own for the self-read or the two
  members made nullable on `HeldGrant`; and `09`'s body for `GET /account/grants` drops
  or nulls the two members.
- Reading C: the export gains the granter and the reason. Smallest fix: two keys in
  `ExportSource.Conferred`; it reverses the export's stated rule.

**Q2 (Tier 3).** What a holder of a declared permission is answered where a restriction
or a step-up gate stops that permission.

- D-276 says the permission held is judged as the single check judges it, a
  restriction and a step-up gate bound to it applying as for any check. It also says
  anything else is refused `authz.denied` and recorded as today.
- The single check answers a restricted account `authz.restricted` for a modifying
  permission, and a session that has not met the gate the step-up's own code; neither
  is recorded.
- Built: such a permission admits nothing, and the question falls to `grant:read`'s
  check, refused `authz.denied` and recorded. This is the composite pattern of
  `AdmitAsync` and `ReadableAsync`, and the more closed of the two readings.
- The other reading: the question is answered the restriction's or the step-up's own
  code, recording nothing, where no other permission admits it.
- The change between them is confined to `AccessGate.AskerRefusedAsync`.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5222 in eleven projects), and
  the commit-message, changelog, release, truth-table change, acceptance-criterion test
  name and forbidden-marker gates pass over `origin/main..HEAD`. The full-history secret
  scan finds nothing. The truth table, `ReverseLookupTests`, `StartupValidationTests`,
  `ExportOperationsTests` and `RefusalRecordTests` were run here against PostgreSQL.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
