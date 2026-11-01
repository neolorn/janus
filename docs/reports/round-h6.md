# Round H6: round H5's questions, and a leaver's own end

## 1. Items implemented

Items are D-239's two and D-240's, as the chapters read at 41c1f758, merged into this
branch.

- D-239, invitations naming a role whose grants must end (Q21, Q23; AUTHZ-GRANT-003
  AC8, REG-INV-001):
  - `grantEndsAt` on `POST /admin/organizations/{id}/invitations` and
    `InvitationRequest.GrantEndsAt`; without it, where a role named has
    `grantsMustEnd`, the issue is refused `422` `authz.grant.endrequired` naming the
    role in `details.role`; one not later than the invitation's own expiry, or given
    where no role named must end, `400` `api.request.malformed` naming `grantEndsAt`;
    nothing is written either way, and the rule is judged again under the roles' locks;
  - the invitation keeps the end and the roles it applies to (migration
    `20261101060000_AddInvitationGrantEnd`, columns `grant_ends_at` and
    `grant_ending_roles`, both or neither); the acknowledgement grants those roles
    ending then and the others open-ended; an invitation issued before holds neither
    and attaches as issued;
  - `GET /account/invitation` and `GET /admin/organizations/{id}/invitations` answer
    `grantEndsAt`, null where the invitation carries none;
  - `InvitationServiceTests.AUTHZ_GRANT_003_AC8_AnInvitationNamingAMustEndRoleCarriesItsEndAsync`,
    `InvitationServiceTests.AUTHZ_GRANT_003_AC8_TheEndAttachesWithTheMustEndRolesAloneAsync`,
    `InvitationEndpointTests.AUTHZ_GRANT_003_AC8_AnInvitationsMustEndRoleTakesItsEndAsync`,
    `MembershipAttachmentTests.AUTHZ_GRANT_003_AC8_TheInvitationsEndIsKeptAndGrantedAsync`
    (integration).
- D-239, older organizations (Q22; OPS-MIG-001 AC6, IDN-ATTR-007):
  - migration `20261101053000_TurnOnTheGrammaticalGenderOfOlderOrganizations` sets
    `grammaticalGender` to `true` in every stored organization override that lacks the
    field, and leaves an override that states it, the system policy and the other keys
    under the prefix as they were;
  - `ConfigurationStoreTests.OPS_MIG_001_TheMigrationTurnsOnTheGrammaticalGenderOfOlderOrganizationsAsync`
    (integration).
- D-240, a member brings their own scheduled end forward (IDN-MEM-001 AC10,
  AUTHZ-GRANT-005 AC9, IDN-ACCT-007):
  - `POST /account/memberships/{organization}/end` and
    `IInvitations.BringMembershipEndForwardAsync`, under the live session alone, with
    no permission and no step-up, a restricted member admitted; it answers `204`;
  - the membership is read again under its row's lock and ended now where its end is
    set and still ahead, and the grants and groups are revoked as the
    `membership-ends` job revokes them, judged against no hand-out list, in the
    member's name with the reason `broughtforward`; a corporate address of the
    administrative organization is retired as the job retires it; no session ends;
  - no end set answers `409` `identity.membership.noend`; an end already past, or no
    membership, `404` `identity.membership.notfound`; the reserved account's
    membership of the administrative organization `403` `authz.denied`;
  - `identity.membership.ended` records the member as actor, `reason`
    `broughtforward`, and the end it replaced in `details.endsAt`;
  - `InvitationServiceTests.IDN_MEM_001_AMemberBringsTheirScheduledEndForwardAsync`,
    `InvitationServiceTests.IDN_MEM_001_OnlyAnEndSetAndAheadIsBroughtForwardAsync`,
    `InvitationEndpointTests.IDN_MEM_001_AMemberBringsTheirEndForwardAsync`,
    `MembershipEndingTests.IDN_MEM_001_AnEndSetAndAheadIsBroughtForwardAsync`
    (integration).

## 2. Items not implemented

None.

## 3. Resolved by rule

- `src/Janus.Authentication/Invitations/InvitedGrantEnd.cs` and the column
  `grant_ending_roles`: AUTHZ-GRANT-003 says each grant "of such a role" ends at
  `grantEndsAt` without saying whether the role is judged at issue or at attach. Rule
  applied: an invitation attaches as it was issued (AUTHZ-GRANT-003, "attaches as
  issued", D-239), so the roles the end applies to are those whose grants had to end at
  issue, kept with the invitation.

## 4. Open questions

**Q21 (Tier 2), answered by D-239 item 1.** An invitation naming a role whose grants must
end. Built (section 1).

**Q22 (Tier 2), answered by D-239 item 2.** Organizations created before round H5.
Built (section 1).

**Q23 (Tier 2), answered by D-239 as 41c1f758 amends it.** The shape of the end an
invitation carries: one optional member, `grantEndsAt`, for the must-end roles' grants
alone. Built (section 1).

**Q24 (Tier 2), answered.** Who writes D-239's and D-240's chapters: the lead, merged
here from `round-h6-docs` at 41c1f758.

**Q25 (Tier 3), answered by D-240 as 41c1f758 amends it.** The sessions a
brought-forward end ends: none; access ends with the revoked grants.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: 5204, green.
- The round's four integration tests run in the pipeline.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
