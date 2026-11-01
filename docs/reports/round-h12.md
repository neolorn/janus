# Round H12: a mailbox for a current member, the mail server's own accounts, and adopting a held account

## 1. Items implemented

- D-253, a mailbox for a current member of the administrative organization (REG-MAIL-001
  AC9 to AC11, INT-MAIL-006, LIB-API-005, `09`
  `POST /admin/organizations/{id}/members/{account}/mailbox`, `10` sections 1, 5.24,
  5.25 and 5b):
  - the route and `IInvitations.GiveMailboxAsync` reserve the mailbox under the
    invitation's address rules and codes, through one set of rules now shared with the
    invitation's issue, hold it for the member at once, owed `enabled`, and take the
    address on as a verified email, the primary where the account holds no verified email
    of the address's domain, the other emails left unlocked; the set as it stood is told
    `identifier-added` once, `IdentifierAdded` and, where it became the primary,
    `IdentifierPrimaryChanged` are emitted, and the giving is recorded as
    `identity.mailbox.given` with its reason and `formerMailbox`;
  - an address the account itself already holds as an email, verified, unverified or as
    an add it began, stays its one identifier for it: marked verified, the add ended,
    primary by the same rule and counted once against `identifiers.email.max`
    (D-253's third amendment);
  - answered 201 `{ id, address, state }`; refused, writing nothing, with
    `identity.mailbox.held`, `identity.identifier.maximum`,
    `identity.account.stateconflict`, `identity.mailbox.taken` naming `address` (an
    address a member holds, a standing invitation reserves, or another account holds as an
    email or keeps for an undo, never the account's own), `identity.invitation.mailboxheld`,
    `identity.identifier.domainnotallowed`, `api.request.invalid` naming `formerMailbox`,
    404 `identity.membership.notfound` for a non-member and for any `{id}` but the
    administrative organization alike, 403 `authz.denied` for the emergency account
    before the step-up, and 422 `integration.mailserver.unavailable` with
    `details.requires` `integration.mailserver.endpoint`;
  - `InvitationServiceTests.REG_MAIL_001_AC9_ACurrentMemberIsGivenAMailboxAsync`;
  - `InvitationServiceTests.REG_MAIL_001_AC10_AnAdministratorGivesThemselvesAMailboxAsync`;
  - `InvitationServiceTests.REG_MAIL_001_AC10_GivingAMailboxIsRefusedAndWritesNothingAsync`;
  - `InvitationServiceTests.REG_MAIL_001_AC11_AnAddressTheAccountHoldsIsTheMailboxsOneEmailAsync`;
  - `InvitationEndpointTests.REG_MAIL_001_AC9_AMemberIsGivenAMailboxOverTheWireAsync`.
- D-254, the mail server's own accounts (INT-MAIL-001 AC8, INT-MAIL-007 AC10,
  OPS-CFG-004, LIB-EXT-001, LIB-HOST-001, `10` section 4):
  - `integration.mailserver.ownaccounts`, protected, a list, default empty, written by
    `configure`;
  - the listing answers each account's type (individual where `@type` is `User`) and
    name, on `HostedMailbox.Individual` and `HostedMailbox.Name`; reconciliation counts
    neither a group nor an account the list names as `unknown`, and still compares an
    account carrying a known mailbox's identifier;
  - the shipped adapter reads the list at the start beside its endpoint and answers a
    push at a name the list holds `integration.mailserver.conflict`, changing nothing;
  - `MailboxReconciliationTests.INT_MAIL_007_AC10_TheMailServersOwnAccountsAreNotCountedAsync`;
  - `JmapMailServerTests.INT_MAIL_001_AC8_APushAtAnOwnAccountIsAConflictAsync`;
  - `JmapMailServerTests.INT_MAIL_006a_AC3_TheListingAnswersEnabledStateAsync` (now with
    the group listed as not individual);
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC8_TheListingAnswersAGroupAsNotIndividualAndEachNameAsync`
    and `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC8_APushAtAnOwnAccountChangesNothingAsync`
    (integration, against the release INT-MAIL-001 names).
- D-255, `formerMailbox` `adopt` (REG-MAIL-001, REG-MAIL-003 AC9, INT-MAIL-001 AC9,
  INT-MAIL-006 AC1e, LIB-HOST-001, `10` sections 1, 5.24 and 5.44):
  - on an invitation or a given mailbox, `adopt` reserves a new mailbox that carries the
    permission on every push (`MailboxPush.MayAdopt`, the `may_adopt` column, migration
    `20261101124410_AddMailboxAdoption`), is recorded as `identity.mailbox.adopted` with
    its reason, and is refused `api.request.invalid` naming `formerMailbox` where the
    library already holds a mailbox at the address;
  - the adapter's push under it takes over an individual account carrying no mailbox
    identifier, writing the identifier into its description and changing
    `authenticate` alone; an account carrying another identifier, a group, an own
    account and a removal are conflicts; where the server holds none, the push creates
    the mailbox;
  - `InvitationServiceTests.REG_MAIL_003_AC9_UnderAdoptThePushTakesOverTheServersAccountAsync`;
  - `InvitationServiceTests.REG_MAIL_003_AC9_AdoptWhereTheLibraryHoldsAMailboxIsInvalidAsync`;
  - `MailboxStoreTests.REG_MAIL_003_AMailboxReservedToAdoptIsReadSoAsync` (integration);
  - `JmapMailServerTests.INT_MAIL_001_AC9_APushThatMayAdoptTakesOverAnUnmarkedAccountAsync`;
  - `JmapMailServerTests.INT_MAIL_001_AC9_APushThatMayAdoptTakesNothingElseAsync`;
  - `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC9_APushThatMayAdoptTakesOverAnUnmarkedAccountAsync`
    and `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC9_APushThatMayAdoptTakesNoOtherAccountAsync`
    (integration).
- Criteria a test decides only in part: INT-MAIL-001 AC9 and INT-MAIL-006 AC1e say the
  adopted account keeps its mail, aliases and group memberships. The test against the
  server shows the adoption acts on the same account (its identifier unchanged) and
  changes its description and `authenticate` alone, every other permission as it was;
  no mail, alias or group membership is read.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `src/Janus.Core/IInvitations.cs`: `07` and D-253 name the call
  `IMemberships.GiveMailboxAsync`; the library has no `IMemberships`, and every
  membership operation (`EndMembershipAsync`, `SetMembershipEndAsync` and the reads) is
  on `IInvitations`, whose summary names the memberships. Rule: the call is
  `IInvitations.GiveMailboxAsync`, as every membership operation is, since a new public
  interface is not Tier 1. The chapters now name it so (D-253's third amendment).
- `tests/Janus.Hosting.Tests/Bff/EndpointDeclarationTests.cs`: the section rule that
  every `/admin/organizations/{id}` route declares `identity.organization.notfound`
  meets the mailbox route, which D-253's amendment 3 and its `09` row answer
  `identity.membership.notfound` for any `{id}` but the administrative organization.
  Rule: test infrastructure, the least change that lets the route stand under its own
  row: the route is excepted from that one check, naming D-253.

## 4. Open questions

1. Answered by D-253's third amendment (81051aca). An address the member's own account
   already holds as an email was refused `identity.mailbox.taken`; it is now that
   email, marked verified, primary by the same rule and counted once, and
   `identity.mailbox.taken` stays for an address another account holds.
2. Answered by D-253's third amendment. "None from the organization's domain" is the
   given address's domain, as built.
3. Answered by D-253's third amendment. A push at a name
   `integration.mailserver.ownaccounts` lists is refused whether or not the server holds
   an account there, as built.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push, with main merged after round H11: 5313,
  green.
- Commit-message gate, the other gates and the full-history secret scan before the
  push: green.
- The pipeline's first run, on 5271b883, failed three cases of one integration test:
  `JmapMailServerAgainstStalwartTests.INT_MAIL_001_AC6_ADisabledPushChangesAuthenticationAloneAsync`
  still expected a listed account with no name. The expectation is corrected in
  99b74908.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
