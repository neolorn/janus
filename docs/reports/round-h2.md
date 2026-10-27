# Round H2: what a host reads and is told

## 1. Items implemented

Criteria proven by tests that existed before the round, and still pass, are marked "at
base". Where two test classes share a name, the project is given.

**`01` identity**

- IDN-READ-001:
  - AC1:
    - `OrganizationReadTests.IDN_READ_001_AC1_AnAdministrativeHolderListsEveryOrganizationButTheErasedInPagesAsync`;
    - `OrganizationReadTests.IDN_READ_001_AC1_AHolderInAnotherOrganizationListsThatOrganizationAloneAsync`;
    - `OrganizationDirectoryTests.IDN_READ_001_AC1_EveryOrganizationButTheErasedIsReadInPagesAsync`;
    - `OrganizationReadEndpointTests.IDN_READ_001_AC1_TheOrganizationsAreListedInPagesAndOneReadAsync`.
  - AC2: `OrganizationServiceTests.IDN_READ_001_AC2_TheAdministrativeOrganizationIsAnsweredWithNoContextAsync`.
  - Also:
    - `OrganizationReadTests.IDN_READ_001_ACallerHoldingItNowhereIsRefusedOnceAsync`;
    - `OrganizationReadTests.IDN_READ_001_ALimitOutsideOneTo500IsMalformedAsync`;
    - `OrganizationReadTests.IDN_READ_001_AnErasedOrganizationIsReadAsOneNeverHeldAsync`;
    - `OrganizationReadEndpointTests.IDN_READ_001_AnUnreadablePageOrALimitOutOfBoundsIsMalformedAsync`;
    - `OrganizationReadEndpointTests.IDN_READ_001_AnOrganizationNotHeldIsNotFoundAsync`.
- IDN-READ-002:
  - AC1, the event and the membership's invitation:
    - `InvitationServiceTests.IDN_READ_002_AC1_TheMembershipAnInvitationBeganIsAnnouncedNamingItAsync`;
    - `BootstrapTests.IDN_READ_002_AC1_AMembershipBootstrapAttachesNamesNoInvitationAsync`;
    - `MembershipAttachmentTests.IDN_READ_002_AC1_TheMembershipKeepsTheInvitationItBeganFromAsync`;
    - `MembershipAttachmentTests.IDN_READ_002_AC1_AMembershipStandingBeforeTheMigrationIsMatchedToItsInvitationAsync`.
  - AC1, the listing:
    - `OrganizationReadTests.IDN_READ_002_AC1_AMemberWhoJoinedThroughAnInvitationIsListedWithItAsync`;
    - `MembershipAttachmentTests.IDN_READ_002_AC1_AMemberIsListedWithTheInvitationItJoinedThroughAsync`.
  - AC2:
    - `OrganizationReadTests.IDN_READ_002_AC2_AnEndedMembershipIsNotFoundAndOneEndingIsListedWithItsEndAsync`;
    - `OrganizationDirectoryTests.IDN_READ_002_AC2_AnEndedMembershipIsNoneAndOneEndingCarriesItsEndAsync`.
  - AC3:
    - `OrganizationReadTests.IDN_READ_002_AC3_AnInvitationAcknowledgedRevokedOrExpiredIsNotListedAsync`;
    - `InvitationStoreTests.IDN_READ_002_AC3_AnInvitationAcknowledgedRevokedOrExpiredIsNotListedAsync`.
  - Also:
    - `OrganizationReadTests.IDN_READ_002_TheMembersAreReadInPagesAsync`;
    - `OrganizationReadEndpointTests.IDN_READ_002_TheMembersAndStandingInvitationsAreReadAsync`.
- IDN-READ-003, in `AccountReadTests` (Authentication) unless marked:
  - AC1: `IDN_READ_003_AC1_AnAccountReadAnswersNoCredentialDateOfBirthOrPhotoAsync`.
  - AC2: `IDN_READ_003_AC2_AnAccountIsFoundByItsIdentifierInAnotherFormAsync`.
  - AC3: `IDN_READ_003_AC3_TheContactReadAnswersThePrimaryVerifiedEmailAndPhoneAsync`.
  - AC4:
    - `StandingTests.IDN_READ_003_AC4_AWithdrawnConsentReadsWithdrawnAndARestrictedAccountRestrictedAsync`;
    - `AccountAdministrationEndpointTests.IDN_READ_003_AC4_TheStandingIsReadAsThePersonsOwnReadAnswersItAsync`.
  - AC5:
    - `IDN_READ_003_AC5_AnAdministratorSetsAnAdministratorOnlyPreferenceAsync`;
    - `IDN_READ_003_AnAdministratorsSettingIsRefusedAsThePersonsOwnIsAsync`;
    - `AccountAdministrationEndpointTests.IDN_READ_003_AC5_AnAdministratorSetsAnotherPersonsPreferencesAsync`;
    - `AccountReadTests.IDN_READ_003_AC5_AnAdministratorsSettingIsRecordedWithTheReasonAsync` (Hosting).
  - The sessions read:
    - `SessionServiceTests.IDN_READ_003_AnotherAccountsSessionsAreListedWithNoneCurrentAsync`;
    - `SessionRevocationEndpointTests.IDN_READ_003_OneAccountsSessionsAreListedOverTheEndpointAsync`.
  - Also: `AccountAdministrationEndpointTests.IDN_READ_003_AnAccountIsReadFoundAndContactedAsync`.
- IDN-READ-004, in `AccountReadTests` (Hosting) unless marked:
  - AC1:
    - `IDN_READ_004_AC1_AnOrganizationReadsThePeopleTiedToItAloneAsync`;
    - `IDN_READ_004_AC1_AStandingReachesThePeopleTiedToTheOrganizationAloneAsync`;
    - `AccountReadTests.IDN_READ_004_AC1_AnAccountBeyondTheReachIsAnsweredAsNoneAsync` (Authentication);
    - `AccountAdministrationEndpointTests.IDN_READ_004_AC1_AnAccountBeyondTheReachIsAnsweredInIdenticalBytesAsync`;
    - `StandingTests.IDN_READ_004_AStandingBeyondTheReachReadsAsAnAccountNoPersonBearsAsync`.
  - AC2:
    - `IDN_READ_004_AC2_AnAdministrativeHolderReadsAnyAccountAsync`;
    - `AccountReadTests.IDN_READ_004_AC2_AnAdministrativeHolderReachesEveryAccountAsync` (Authentication).
  - AC3:
    - `IDN_READ_004_AC3_AJobReadsContactDetailsOutsideARequestAloneAsync`;
    - `AuthorizationModelTests.IDN_READ_004_AC3_AJobDeclaresTheLibrarysFourReads`;
    - `AuthorizationModelTests.IDN_PRIN_001_AC7_AJobDeclaredWithAPermissionItMayNotExerciseFailsStartup`
      (now with `account:read`).
  - AC4:
    - `IDN_READ_004_AC4_APersonsReadsAddToTheirReadVolumeAndAJobsDoNotAsync`;
    - `AccountReadTests.IDN_READ_004_AC4_EachPersonRecordReadIsCountedAsync` (Authentication).
  - AC4, the members: `OrganizationReadTests.IDN_READ_004_EachMemberAPersonReadsIsCountedAsync`.
  - AC5:
    - `OrganizationReadTests.IDN_READ_004_AC5_AnAdministrativeHolderListsAnotherOrganizationsMembersAsync`;
    - `OrganizationReadTests.IDN_READ_004_AC5_AHolderInNeitherOrganizationIsRefusedOnceAsync`;
    - `OrganizationReadEndpointTests.IDN_READ_004_AC5_TheAdministrativeOrganizationReachesAnotherOrganizationsMembersAsync`;
    - `TruthTableTests.IDN_READ_004_AC5_EveryOrganizationReadDecidesTheWayTheTableSaysThroughTheAdmissionAsync`:
      the new `OrganizationReads` table, each refusal recorded once and naming
      `organization:read` in the organization the path names, or in the administrative
      organization for the list.
  - A job's organization reads, in its own organization alone:
    - `OrganizationReadTests.IDN_READ_004_AJobListsItsOwnOrganizationAsync`;
    - `OrganizationReadTests.IDN_READ_004_AJobReadsItsOwnOrganizationAloneAsync`;
    - the `OrganizationReads` rows "a read of its organization, by a declared job of
      it" (Allowed), "a read of another organization, by a declared job of the
      administrative organization" (Denied) and "the list, by a declared job of another
      organization" (Allowed, listing its own).
  - Also:
    - `AccountReadTests.IDN_READ_004_TheOrganizationAskedInIsFoundAndItsPermissionHeldAsync` (Authentication);
    - `StandingTests.IDN_READ_004_AStandingAskedOutsideTheHoldersOrganizationIsRefusedAsync`;
    - `RoleStoreTests.IDN_READ_004_TheSystemAdministratorTakesTheReadsOnceMigratedAsync`.
- IDN-PRIN-001, as amended:
  - The job's four reads: the IDN-READ-004 AC3 tests above.
  - The truth-table rows of `JobOrganizations`:
    - "a declared job asking a library read its declaration names, of its organization" (Allowed);
    - "... its declaration does not name, of its organization" (Denied);
    - "... names, of its organization while a request is served" (Denied).
  - The rest of the item: at base.
- IDN-LIFE-008, the sessions a password change ends, now announced:
  - `CredentialServiceTests.IDN_LIFE_008_APasswordSetEndsAndAnnouncesTheOtherSessionsAsync`;
  - `RecoveryServiceTests.IDN_LIFE_008_ARecoveredPasswordEndsAndAnnouncesEverySessionAsync`.

**`03` authorization**

- AUTHZ-GATE-007, in process (`IAccessGate.WhoMayAsync`):
  - AC1:
    - `TruthTableTests.AUTHZ_GATE_007_AC1_EveryCaseNamesTheAccountWhereTheCheckAdmitsItAsync`:
      every row of the main table; group, container, derivation and organization-wide
      holders are named, and deny, expiry and revocation are not;
    - `TruthTableTests.AUTHZ_GATE_007_AC1_EveryAdmissionCaseNamesTheAccountTheWayTheTableSaysAsync`:
      the new `Admissions` table, covering a suspended account, organization-wide grants
      and an ended membership of the administrative organization;
    - `ReverseLookupTests.AUTHZ_GATE_007_AC1_AStoredAndADerivedHolderAreNamedAsync`.
  - AC2: `TruthTableTests.AUTHZ_GATE_007_AC2_EveryRestrictedCaseNamesTheAccountWhereTheCheckAdmitsItAsync`.
  - AC3:
    - `ReverseLookupTests.AUTHZ_GATE_007_AC3_AJobIsAnsweredForAPermissionItDeclaresAloneAsync`;
    - `ReadyNoticesTests.LIB_TEST_004_AJobReadsTheContactDetailsOfEveryoneWhoMayReadARepairAsync` (sample host).
  - AC4: `ReverseLookupTests.AUTHZ_GATE_007_AC4_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync`.
  - The consent rule: `TruthTableTests.AUTHZ_GATE_007_EveryConsentCaseNamesTheAccountWhereTheCheckAdmitsItAsync`.
  - Refusing who may ask: `ReverseLookupTests.AUTHZ_GATE_007_ACallerWithoutGrantReadAndAnUnregisteredRecordAreRefusedAsync`.
  - AC5:
    - `ReverseLookupTests.AUTHZ_GATE_007_AC5_AWordTheModelDoesNotDeclareIsMalformedAndRecordsNothingAsync`;
    - `AccessEndpointTests.AUTHZ_GATE_007_AC5_AWordTheModelDoesNotDeclareIsAnsweredAsMalformedAsync`;
    - the `Admissions` rows for an undeclared type and permission, now answered malformed.
  - Over `GET /admin/access/admitted`:
    - `AccessEndpointTests.AUTHZ_GATE_007_TheAccountsWhoMayAreAnsweredOverTheEndpointAsync`;
    - `AccessEndpointTests.AUTHZ_GATE_007_TheAccountsWhoMayAreRefusedWithoutGrantReadAsync`.
  - Truth-table row for the change to the gate: "an account holding the permission
    through a grant on the record's container" (Allowed).

**`07` the library contract**

- LIB-API-005, the operations this round adds to the contract, each with its endpoint
  where `09` section 8a gives one:
  - `IAccounts`: `ReadAsync`, `FindAsync`, `ContactAsync` and `SetPreferencesAsync`;
  - `IConsents.StandingAsync`;
  - `ISessions.ListOfAsync`;
  - `IOrganizations`: `AdministrativeAsync`, `ListAsync` and `ReadAsync`;
  - `IInvitations`: `MembersAsync`, `MemberAsync` and `StandingAsync`;
  - `IAccessGate.WhoMayAsync`.
  - Proven by the item tests above, `IdentityEndpointsTests` (at base) and
    `EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract` with its baseline.
- LIB-HOST-001 (jobs): the four reads a job declares, proven by the IDN-READ-004 AC3 tests.
- LIB-TEST-004: the sample host's declared job `ready-notices` asks which accounts may
  read a repair and reads each one's contact details outside a request:
  `ReadyNoticesTests.LIB_TEST_004_AJobReadsTheContactDetailsOfEveryoneWhoMayReadARepairAsync`.
- LIB-TEST-004: the sample host's staff page lists the current members of the
  organization that runs a workshop:
  `WorkshopEndpointTests.LIB_TEST_004_AC2_TheStaffPageListsTheOrganizationsMembersAsync`.

**`09` section 8a, the reads**

- Built:
  - `GET /admin/accounts/{subject}`;
  - `POST /admin/accounts/find`;
  - `GET /admin/accounts/{subject}/contact`;
  - `GET /admin/accounts/{subject}/standing`;
  - `GET /admin/accounts/{subject}/sessions`;
  - `PUT /admin/accounts/{subject}/preferences`;
  - `GET /admin/organizations` and `GET /admin/organizations/{id}`;
  - `GET /admin/organizations/{id}/members` and `/members/{subject}`;
  - `GET /admin/organizations/{id}/invitations`;
  - `GET /admin/access/admitted`.
- Proven by the endpoint tests named under IDN-READ-001 to IDN-READ-004 and
  AUTHZ-GATE-007, and by the endpoint baseline.

**`10` the reference**

- Section 2.1: `organization:read`, `membership:read`, `account:read`, `contact:read`,
  `consent:read`, with the rows that changed. Proven by:
  - `PermissionsTests` (at base, extended);
  - `RoleStoreTests.IDN_READ_004_TheSystemAdministratorTakesTheReadsOnceMigratedAsync`.
- Section 5.24: `identity.preferences.changed` with `details.reason` for an
  administrator's setting. Proven by
  `AccountReadTests.IDN_READ_003_AC5_AnAdministratorsSettingIsRecordedWithTheReasonAsync` (Hosting).
- Sections 5.39 and 5b, each event raised in the transaction that makes it true:
  - `OrganizationCreated`, `OrganizationDeletionRequested`, `OrganizationDeletionCancelled`:
    - `OrganizationServiceTests.LIB_API_001_ACreatedOrganizationIsAnnouncedInItsTransactionAsync`;
    - `..._ADeletionRequestAndItsCancellationAreAnnouncedInTheirTransactionsAsync`;
    - `..._CONV_DESIGN_002_AnOrganizationWhoseEventIsNotTakenIsNotCreatedAsync`.
  - `RoleChanged`:
    - `RoleEndpointTests.LIB_API_001_ARoleDefinedChangedAndRemovedIsAnnouncedAtEachAsync`;
    - bootstrap's three roles raise none:
      `BootstrapTests.LIB_API_001_TheRolesBootstrapDefinesAreNotAnnouncedAsync`.
  - `GroupMembershipChanged`: `GroupEndpointTests.LIB_API_001_AMemberAddedAndTakenOutOfAGroupIsAnnouncedAtEachAsync`.
  - `GrantChanged`:
    - `GrantEndpointTests.LIB_API_001_AGrantMadeAndRevokedIsAnnouncedAtEachAsync`;
    - `MembershipAttachmentTests.LIB_API_001_EachGrantAnAcknowledgementWritesIsAnnouncedAsync`.
  - `SessionEnded`:
    - a sign-out `signedout` and an end from the list `ended`:
      `SessionServiceTests.LIB_API_001_AnEndFromTheListIsEndedAndASignOutSignedOutAsync`;
    - a refresh token presented twice, `security`:
      `OidcServiceTests.LIB_API_001_EachSessionAReuseEndsIsAnnouncedAsSecurityAsync`;
    - the provider's security event, `security`:
      `ProviderEventTests.IDN_LIFE_012a_AC1_ASignedCompromiseEndsEverySessionAndHoldsTheCredentialAsync`;
    - a restriction of processing, `account`:
      `AccountStatesTests.AUTH_SESS_010_AC4_RestrictingAnAccountEndsItsSessionsAsync`;
    - the reasons, `security` among them:
      `VocabularyContractTests.LIB_API_001_TheChangesTheEventsCarryAreTheContract`;
    - `SessionServiceTests.LIB_API_001_EachSessionARevocationEndsIsAnnouncedAsync`;
    - `SessionServiceTests.LIB_API_001_ASessionPastItsExpiryIsNotAnnouncedAsync`;
    - `SessionServiceTests.CONV_DESIGN_003_AC5_ARevocationWhoseEndIsNotAnnouncedIsRolledBackAsync`;
    - `SessionStoreTests.AUTH_SESS_010_AnEndHandsBackTheSessionsItEndedBeforeTheirExpiryAsync`;
    - the IDN-LIFE-008 tests above.
  - `MembershipChanged.invitation`: the IDN-READ-002 AC1 tests above.
  - The changes' vocabulary: `VocabularyContractTests.LIB_API_001_TheChangesTheEventsCarryAreTheContract`.
  - Truth-table rows restating each unchanged decision:
    - "a member added to and taken out of a group, each announced in its transaction, by a caller managing groups";
    - "a role defined, changed and removed, each announced in its transaction, by a caller managing roles";
    - "a grant written and then revoked, each announced in its transaction, by a caller managing grants".

**`12` disaster recovery**

- DR-016, the replay of the erasure ledger, which failed in the pipeline's run on the
  previous push for want of the event outbox a takedown writes to: `ErasureReplayTests`
  (Cli), at base, now composed with it.

**The documents taken in**

Each file's `git hash-object` was checked against the owner's message.

- D-218 (`f030cb09`): the chapters it names, the plan and the decision log.
- D-219 (`c3a39214`): the chapters it names, the plan and the decision log.
- D-220 (`666c736e`): the plan and the decision log. The round builds nothing from it.

## 2. Items not implemented

None.

## 3. Resolved by rule

- `SessionEnded`, "ended before its expiry" (`10` section 5b):
  - What was out of step: a bulk end also reaches sessions already past a deadline,
    which no instant ended.
  - Governing item: AUTH-SESS-005.
  - Rule: a session is announced only where both its inactivity deadline and its
    absolute deadline are still ahead.
- The `invitation` of a membership that stood before the migration (`10` section 5b,
  `MembershipChanged`):
  - What was out of step: no row named the invitation.
  - Governing item: IDN-READ-002 AC1.
  - Rule: the migration matches the membership by invitee, organization and the
    acknowledged instant being equal.
- `legalName` in the account read (`09` section 8a, IDN-READ-003 AC1):
  - What was out of step: the chapter says the member is absent while
    `profile.legalname` is off.
  - Governing item: API-CONV-002 (a response member the endpoint carries is written,
    null where it holds nothing).
  - Rule: the member is written as null, as the account's own profile read writes it.
- The optional `organization`, `after` and `limit` query values of the reads (`09`
  section 8a):
  - What was out of step: no binding can give an optional value a malformed answer.
  - Governing item: `10` section 1, `api.request.malformed`.
  - Rule: the value is read as text and an unreadable one answers malformed, naming
    `organization`, `after` or `limit`; a `limit` that reads but lies outside 1 to 500
    is the read's to refuse, naming `limit`.
- A read naming no organization before bootstrap (IDN-READ-004):
  - What was out of step: no administrative organization exists to ask it in.
  - Governing item: AUTHZ-SCOPE-001.
  - Rule: it is refused `authz.denied`, as a scope that names no organization is.
    The list of organizations is refused alike, recording nothing.
- The account finding and read volume (IDN-READ-004, OPS-ALERT-005):
  - What was out of step: whether a finding counts.
  - Governing item: IDN-READ-004.
  - Rule: a finding answers a subject and no person record, and the item counts
    person records only (an account, a contact, a standing, a member), so it is not
    counted.
- A deleted account in the sessions read and the preference setting:
  - What was out of step: neither row names a deleted account.
  - Governing item: IDN-READ-003.
  - Rule: as for every read of IDN-READ-003, it answers `identity.account.notfound`.

- Standing invitations and read volume (IDN-READ-004, OPS-ALERT-005):
  - What was out of step: whether an invitation read counts.
  - Governing item: IDN-READ-004.
  - Rule: an invitation is none of the person records the item counts (an account, a
    contact, a standing, a member), so it is not counted.
- A job's reads under an organization's path (IDN-READ-001, IDN-READ-004, D-219):
  - What was out of step: the path rule admits the permission held in the
    administrative organization, which read alone would admit a job acting for that
    organization in every organization.
  - Governing item: IDN-READ-004, a job "admitted for the reads they govern in its own
    organization", and IDN-READ-001, the list answering "a job its own organization".
  - Rule: a job is admitted for these reads in its own organization alone.
- The gate step of the reads (CONV-DESIGN-002 AC3, `PublicSurfaceTests`):
  - What was out of step: the test lists the collaborators a call on which is the gate
    step, naming the grant admission and not the read admission D-219 makes one.
  - Governing item: IDN-READ-004 (D-219), the read admitted as one admission "as
    `grant:manage` with `system:administer` is".
  - Rule: the read admission is listed beside the grant admission, the least change to
    the test that lets the reads meet it; the reads ask it first, past the scope
    resolution of the organization the path names.

## 4. Open questions

**Q1 (Tier 2, answered by D-219: reading A).** Which organization the
organization-path reads are asked in, and how the administrative organization's reach
meets a path naming another organization (IDN-READ-001, IDN-READ-002, IDN-READ-004,
`09` section 8a).

- What the code needs:
  - `GET /admin/organizations/{id}`, `/members`, `/members/{subject}` and
    `/invitations` name organization Y in the path. Asked in Y, an operator holding the
    permission only in the administrative organization is refused.
  - `GET /admin/organizations` names no organization, so a holder elsewhere is asked in
    the administrative organization and refused, against IDN-READ-001 AC1.
- Reading A, one admission:
  - The permission held in Y, or the same permission in the administrative
    organization, admits; one refusal naming Y is recorded where neither does
    (AUTHZ-SCOPE-001, D-208).
  - The list answers every organization to a holder in the administrative
    organization, and their own organizations to others.
  - Smallest fix: an internal admission beside the grant admission; `09` unchanged.
- Reading B: each read takes `?organization` (the organization asked in, the
  administrative one where absent), and `{id}` is the organization read.
  - Smallest fix: `09`'s five rows gain `?organization={id}`, and the in-process
    signatures take it.
- Parks:
  - `IOrganizations.ListAsync` and `ReadAsync`;
  - `IInvitations.MembersAsync`, `MemberAsync` and `StandingAsync`, with their endpoints;
  - the sample host's members page.
- Built: the parked items, under reading A.

**Q2 (Tier 2, answered by D-219: reading B).** Whether bootstrap's definitions of
`system-administrator`, `auditor` and `support` raise `RoleChanged` `defined` (`10`
section 5b).

- Reading A: yes. Smallest fix: the deployment seed returns the roles it created, and
  bootstrap publishes one event for each.
- Reading B: no, as `OrganizationCreated` is limited to `POST /admin/organizations`.
- Parks: `RoleChanged` at bootstrap.
- Built: bootstrap's three roles raise no `RoleChanged`; a declared role's seeding
  still raises one.

**Q3 (Tier 2, answered by D-219: reading C).** `signedout` or `ended` for a person's
own sign-out (`10` section 5.39, `SessionEnded`).

- What the code needs: a reason for each session `ISessions.EndAsync` ends and each one
  `EndEverywhereAsync` ends (AUTH-SESS-008). Neither knows which session is asking, and
  both are public.
- Reading A: the asking session `signedout`, the others `ended`, in both. Smallest fix:
  a `SessionId` parameter on both.
- Reading B: logout gives `ended` to all, and `EndAsync` gives `signedout` to the asking
  session. Smallest fix: the parameter on `EndAsync` alone.
- Reading C: logout gives `signedout` to all, and `EndAsync` gives `ended`. Smallest fix:
  no surface change.
- Parks: `SessionEnded` from these two operations.
- Built: `EndEverywhereAsync` gives `signedout` to every session it ends, and
  `EndAsync` gives `ended` to the session it names; the public surface is unchanged.

**Q4 (Tier 2, answered by D-219).** Three ends no reason of `10` section 5.39 names.

- (a) A refresh token presented twice ends its session family (AUTH-OIDC-003). Readings:
  `revoked`, or a new reason.
- (b) A provider's event ends every session and holds the credential (IDN-LIFE-012a
  AC1). Readings: `ended`, `account`, or a new reason.
- (c) A restriction of processing ends every session (PRIV-RIGHT-004, IDN-ACCT-007).
  Readings: `account`, or a new reason.
- Smallest fix for each: one line of section 5.39 naming the reason.
- Parks: `SessionEnded` from these three paths.
- Built: (a) and (b) end their sessions with the new reason `security`; (c) ends them
  with `account`.

**Q5 (Tier 2, answered by D-219: reading B).** How `WhoMayAsync` and
`GET /admin/access/admitted` answer a word the model does not declare (AUTHZ-GATE-007,
`10` section 1, LIB-API-005).

- What the code needs: an answer for an undeclared permission, an undeclared type, and
  an organization identifier that is not a UUID.
- What the spec says:
  - AUTHZ-GATE-007: an undeclared permission is refused "as the single check refuses
    it", and the check raises (CONV-ERR-001).
  - `10` section 1: a query value holding an undeclared permission answers
    `api.request.malformed`.
  - LIB-API-005 AC1 and AC3: the endpoint takes one service and carries no logic, so it
    cannot read the model to answer 400 itself.
- Reading A: `WhoMayAsync` raises for an undeclared permission or type, as the check
  does. Over HTTP the word then reaches no 400. Smallest fix: `09`'s row says what the
  endpoint answers.
- Reading B: `WhoMayAsync` answers `api.request.malformed` naming `permission`, `type`
  or `id`, as `WhoCanAccessAsync` answers `resourceType` and `resourceId`, and as
  `IRoles` answers `permissions`. The endpoint stays one line. Smallest fix: AUTHZ-GATE-007
  reads "refused as malformed" for the three.
- Parks: `GET /admin/access/admitted`.
- Built: `WhoMayAsync` answers an undeclared permission or type, and an organization
  identifier that is not one, `api.request.malformed` naming `permission`, `type` or
  `id`, recording no refusal; the endpoint maps it in one line.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. These are every unit test, the commit-message gate and
  the full-history secret scan.
- The gate of record is the pipeline's run on this report's commit. The run's last
  message gives its identifier and result.
