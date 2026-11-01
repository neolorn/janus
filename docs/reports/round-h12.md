# Round H12: a mailbox for a current member, the mail server's own accounts, and adopting a held account

## 1. Items implemented

- D-253, a mailbox for a current member of the administrative organization (REG-MAIL-001
  AC9 and AC10, INT-MAIL-006, LIB-API-005, `09`
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
  - answered 201 `{ id, address, state }`; refused, writing nothing, with
    `identity.mailbox.held`, `identity.identifier.maximum`,
    `identity.account.stateconflict`, `identity.mailbox.taken` naming `address` (an
    address a member holds, a standing invitation reserves, or another account holds as an
    email or keeps for an undo), `identity.invitation.mailboxheld`,
    `identity.identifier.domainnotallowed`, `api.request.invalid` naming `formerMailbox`,
    404 `identity.membership.notfound` for a non-member and for any `{id}` but the
    administrative organization alike, 403 `authz.denied` for the emergency account
    before the step-up, and 422 `integration.mailserver.unavailable` with
    `details.requires` `integration.mailserver.endpoint`;
  - `InvitationServiceTests.REG_MAIL_001_AC9_ACurrentMemberIsGivenAMailboxAsync`;
  - `InvitationServiceTests.REG_MAIL_001_AC10_AnAdministratorGivesThemselvesAMailboxAsync`;
  - `InvitationServiceTests.REG_MAIL_001_AC10_GivingAMailboxIsRefusedAndWritesNothingAsync`;
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

- The lead's two additions to the round, which AGENTS.md sections 6 and 9 leave without
  a chapter until the lead commits one: the front-channel probe of LIB-TEST-001 AC4 (a
  direct authorization request from the sign-on client is refused), decided as the
  answer to round H11's third question, and registration refusing `offline_access`
  among a `public-application` client's scopes (`api.request.malformed` naming
  `scopes`). The message asked the run to write their chapter lines; the documents are
  the lead's, so none was written, and both wait for the lines (section 4, question 1).

## 3. Resolved by rule

- `src/Janus.Core/IInvitations.cs`: `07` and D-253 name the call
  `IMemberships.GiveMailboxAsync`; the library has no `IMemberships`, and every
  membership operation (`EndMembershipAsync`, `SetMembershipEndAsync` and the reads) is
  on `IInvitations`, whose summary names the memberships. Rule: the call is
  `IInvitations.GiveMailboxAsync`, as every membership operation is, since a new public
  interface is not Tier 1.
- `tests/Janus.Hosting.Tests/Bff/EndpointDeclarationTests.cs`: the section rule that
  every `/admin/organizations/{id}` route declares `identity.organization.notfound`
  meets the mailbox route, which D-253's amendment 3 and its `09` row answer
  `identity.membership.notfound` for any `{id}` but the administrative organization.
  Rule: test infrastructure, the least change that lets the route stand under its own
  row: the route is excepted from that one check, naming D-253.

## 4. Open questions

1. Open, for the lead. The two additions in section 2 need their chapter lines: in `07`
   LIB-TEST-001 AC4 (the direct request among the forms the suite asks for), and under
   D-252 in `02` AUTH-OIDC-001 or `06` OPS-BOOT-001 (the registration's refusal). Both
   are parked until the lines are committed on this branch.
2. Open, Tier 2. D-253 refuses an address another account holds as an email, and says
   nothing of an address the member's own account already holds as an email (for
   instance an administrator bootstrapped with the company address as the personal
   one). The code refuses it `identity.mailbox.taken` naming `address`, writing
   nothing. Readings: (a) refused, as built; (b) the mailbox is given and the email the
   account holds stays the one identifier at the address, verified, made primary by the
   same rule. Smallest fix for (b): `MailboxGiving` skips the take-on where the member
   holds the address, verifying it where it is pending.
3. Open, Tier 2. D-253: the address becomes the primary "where the account holds none
   from the organization's domain". The code reads it as the given address's domain.
   The other reading is any domain of the organization's lock. Smallest fix for the
   second: compare against the lock's domains where the organization lists any.
4. Open, Tier 2. D-254: a push "that meets an account" the list names is a conflict. The
   shipped adapter refuses a push at a name the list holds whether or not the server
   holds an account there, so no mailbox is created at a name the deployment keeps
   (D-254, "a mailbox can never be given an address the deployment keeps for itself").
   The other reading creates the mailbox where no account stands. Smallest fix: the
   adapter asks the list only once it has found the account.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push, with main merged after round H11: 5310,
  green.
- Commit-message gate, the other gates and the full-history secret scan before the
  push: green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
