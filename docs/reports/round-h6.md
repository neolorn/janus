# Round H6: round H5's questions

## 1. Items implemented

Items are D-239's two and D-240's, as the decision log reads at 56df7a0f, merged into
this branch. No chapter of `docs/spec/` carries D-239 or D-240 yet (section 4, Q24).

- D-239, older organizations (Q22; OPS-MIG-001, IDN-ATTR-007):
  - migration `20261101053000_TurnOnTheGrammaticalGenderOfOlderOrganizations` sets
    `grammaticalGender` to `true` in every stored organization override that lacks the
    field, and leaves an override that states it, the system policy and the other keys
    under the prefix as they were;
  - `ConfigurationStoreTests.OPS_MIG_001_TheMigrationTurnsOnTheGrammaticalGenderOfOlderOrganizationsAsync`
    (integration).
- D-240, a member brings their own scheduled end forward (IDN-MEM-001,
  AUTHZ-GRANT-005):
  - `POST /account/memberships/{organization}/end` and
    `IInvitations.BringMembershipEndForwardAsync`, under the live session alone, with
    no permission and no step-up; it answers `204`;
  - the membership is read again under its row's lock and ended now where its end is
    set and still ahead, and the grants and groups are revoked as the
    `membership-ends` job revokes them, judged against no hand-out list, in the
    member's name with the reason `broughtforward`; a corporate address of the
    administrative organization is retired as the job retires it;
  - no end set answers `409` `identity.membership.noend`; an end already past, or no
    membership, `404` `identity.membership.notfound`; the reserved account's
    membership of the administrative organization `403` `authz.denied`, as the job
    keeps it;
  - `identity.membership.ended` records the member as actor, `reason`
    `broughtforward`, and the end it replaced in the detail `endsAt`, the name
    `identity.membership.endset` gives the same instant;
  - `InvitationServiceTests.IDN_MEM_001_AMemberBringsTheirScheduledEndForwardAsync`,
    `InvitationServiceTests.IDN_MEM_001_OnlyAnEndSetAndAheadIsBroughtForwardAsync`,
    `InvitationEndpointTests.IDN_MEM_001_AMemberBringsTheirEndForwardAsync`,
    `MembershipEndingTests.IDN_MEM_001_AnEndSetAndAheadIsBroughtForwardAsync`
    (integration).

## 2. Items not implemented

- D-239, invitations naming a role whose grants must end (Q21): parked on Q23.
- D-240, "The member's sessions in that organization's applications end": parked on
  Q25. The rest of D-240 is built.

## 3. Resolved by rule

None.

## 4. Open questions

**Q21 (Tier 2), answered by D-239 item 1.** An invitation naming a role whose grants must
end. Parked its build on Q23 below.

**Q22 (Tier 2), answered by D-239 item 2.** Organizations created before round H5.
Built (section 1).

**Q23 (Tier 2), open.** The shape of the end an invitation carries.

- What the code needs: the request member that carries the grant's end on
  `POST /admin/organizations/{id}/invitations` and the library call, and where it is
  held.
- What the decision says: the invitation "must carry that grant's `expiresAt`, a time
  after the invitation's own expiry". The invitation's answer already names its own
  expiry `expiresAt` (`09`, `{ id, expiresAt, token }`), and `roles` is a list, so one
  invitation may name several roles whose grants must end.
- Reading A: one body member for the whole invitation (for example `grantsExpireAt`),
  the end of every grant of a must-end role it names. Smallest fix: a sentence and a
  member in `09`.
- Reading B: an end per role, `roles` taking entries of `{ role, expiresAt }`. Smallest
  fix: a change of the member's shape in `09` and of the stored invitation.
- Reading C: one end for every grant the invitation makes, must-end or not. Smallest
  fix: a sentence and a member in `09`.
- Parks: D-239 item 1 whole.

**Q24 (Tier 2), open.** Who writes D-239's chapters.

- What was asked: the chapters of D-239's "Propagated to" line written on this branch as
  `docs:` commits.
- What the instructions say: `AGENTS.md` sections 6 and 9, the documents change only by
  the lead's commits, and the implementer never modifies, stages or commits a file under
  `docs/spec/`, `docs/guide/` or `docs/decision-log.md`.
- Parks: the chapters. One check waits on them: `identity.membership.noend` has no row
  in `10` yet, so `ErrorCodesTests.REF_001_AC1_EveryCodeInTheSourceIsARowOfTheReference`
  and `ErrorCodesTests.BFF_ERR_001_AC3_EveryCodeTheBoundaryCanAnswerIsInTheReference`
  fail on this branch until `10` section 1 carries it.

**Q25 (Tier 3), open.** The sessions a brought-forward end ends.

- What the code needs: which sessions end when a member brings their end forward.
- What the decision says: "The member's sessions in that organization's applications
  end." It also says the end is "the same end as the `membership-ends` job's", and
  neither the job nor an administrator's end ends any session today.
- What the code has: a session belongs to the account, not to an organization. The
  session store ends one spine, every session of an account, or every session; it holds
  no end by organization, and nothing ties an application, or the tokens it holds, to
  one organization's sessions.
- Parks: the session clause of D-240 only.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: 5202, of which 5200 pass. The two that fail
  are the reference checks of `ErrorCodesTests` named under Q24, which wait for
  `identity.membership.noend`'s row in `10`.
- The round's two integration tests run first in the pipeline.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
