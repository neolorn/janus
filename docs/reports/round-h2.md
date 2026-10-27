# Round H2: what a host reads and is told

## 1. Items implemented

Criteria proven by tests that existed before the round, and still pass, are marked "at
base". Where two test classes share a name, the project is given.

**`01` identity**

- IDN-READ-001:
  - AC2: `OrganizationServiceTests.IDN_READ_001_AC2_TheAdministrativeOrganizationIsAnsweredWithNoContextAsync`.
- IDN-READ-002:
  - AC1, the event and the membership's invitation:
    - `InvitationServiceTests.IDN_READ_002_AC1_TheMembershipAnInvitationBeganIsAnnouncedNamingItAsync`;
    - `BootstrapTests.IDN_READ_002_AC1_AMembershipBootstrapAttachesNamesNoInvitationAsync`;
    - `MembershipAttachmentTests.IDN_READ_002_AC1_TheMembershipKeepsTheInvitationItBeganFromAsync`;
    - `MembershipAttachmentTests.IDN_READ_002_AC1_AMembershipStandingBeforeTheMigrationIsMatchedToItsInvitationAsync`.
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

**`07` the library contract**

- LIB-API-005, the operations this round adds to the contract, each with its endpoint
  where `09` section 8a gives one:
  - `IAccounts`: `ReadAsync`, `FindAsync`, `ContactAsync` and `SetPreferencesAsync`;
  - `IConsents.StandingAsync`;
  - `ISessions.ListOfAsync`;
  - `IOrganizations.AdministrativeAsync`;
  - `IAccessGate.WhoMayAsync`.
  - Proven by the item tests above, `IdentityEndpointsTests` (at base) and
    `EndpointContractTests.LIB_API_001_AC2_TheEndpointsAreTheContract` with its baseline.
- LIB-HOST-001 (jobs): the four reads a job declares, proven by the IDN-READ-004 AC3 tests.
- LIB-TEST-004: the sample host's declared job `ready-notices` asks which accounts may
  read a repair and reads each one's contact details outside a request:
  `ReadyNoticesTests.LIB_TEST_004_AJobReadsTheContactDetailsOfEveryoneWhoMayReadARepairAsync`.

**`09` section 8a, the reads**

- Built:
  - `GET /admin/accounts/{subject}`;
  - `POST /admin/accounts/find`;
  - `GET /admin/accounts/{subject}/contact`;
  - `GET /admin/accounts/{subject}/standing`;
  - `GET /admin/accounts/{subject}/sessions`;
  - `PUT /admin/accounts/{subject}/preferences`.
- Proven by the endpoint tests named under IDN-READ-003 and IDN-READ-004, and by the
  endpoint baseline.

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
  - `RoleChanged`: `RoleEndpointTests.LIB_API_001_ARoleDefinedChangedAndRemovedIsAnnouncedAtEachAsync`.
  - `GroupMembershipChanged`: `GroupEndpointTests.LIB_API_001_AMemberAddedAndTakenOutOfAGroupIsAnnouncedAtEachAsync`.
  - `GrantChanged`:
    - `GrantEndpointTests.LIB_API_001_AGrantMadeAndRevokedIsAnnouncedAtEachAsync`;
    - `MembershipAttachmentTests.LIB_API_001_EachGrantAnAcknowledgementWritesIsAnnouncedAsync`.
  - `SessionEnded`, on the paths no question parks:
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

## 2. Items not implemented

- IDN-READ-001 AC1, and `IOrganizations.ListAsync` and `ReadAsync` with their endpoints.
  Wait on Q1.
- IDN-READ-002, the reads waiting on Q1:
  - AC1's listing half, AC2 and AC3;
  - `IInvitations.MembersAsync`, `MemberAsync` and `StandingAsync`, with their endpoints.
- The sample host's staff page listing its organization's members (LIB-TEST-004, D-218
  rule 11). Waits on Q1.
- `RoleChanged` for the roles bootstrap defines. Waits on Q2.
- `SessionEnded` from:
  - `ISessions.EndAsync` and `EndEverywhereAsync` (waits on Q3);
  - the refresh-token replay, the provider's event and the restriction of processing
    (waits on Q4).
- `GET /admin/access/admitted` (AUTHZ-GATE-007, `09` section 8a). Waits on Q5.

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
- The optional `organization` query value of the reads (`09` section 8a):
  - What was out of step: no binding can give an optional identifier a malformed answer.
  - Governing item: `10` section 1, `api.request.malformed`.
  - Rule: the value is read as text and an unreadable one answers malformed, naming
    `organization`.
- A read naming no organization before bootstrap (IDN-READ-004):
  - What was out of step: no administrative organization exists to ask it in.
  - Governing item: AUTHZ-SCOPE-001.
  - Rule: it is refused `authz.denied`, as a scope that names no organization is.
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

## 4. Open questions

**Q1 (Tier 2, unanswered).** Which organization the organization-path reads are asked
in, and how the administrative organization's reach meets a path naming another
organization (IDN-READ-001, IDN-READ-002, IDN-READ-004, `09` section 8a).

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

**Q2 (Tier 2, unanswered).** Whether bootstrap's definitions of `system-administrator`,
`auditor` and `support` raise `RoleChanged` `defined` (`10` section 5b).

- Reading A: yes. Smallest fix: the deployment seed returns the roles it created, and
  bootstrap publishes one event for each.
- Reading B: no, as `OrganizationCreated` is limited to `POST /admin/organizations`.
- Parks: `RoleChanged` at bootstrap.

**Q3 (Tier 2, unanswered).** `signedout` or `ended` for a person's own sign-out (`10`
section 5.39, `SessionEnded`).

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

**Q4 (Tier 2, unanswered).** Three ends no reason of `10` section 5.39 names.

- (a) A refresh token presented twice ends its session family (AUTH-OIDC-003). Readings:
  `revoked`, or a new reason.
- (b) A provider's event ends every session and holds the credential (IDN-LIFE-012a
  AC1). Readings: `ended`, `account`, or a new reason.
- (c) A restriction of processing ends every session (PRIV-RIGHT-004, IDN-ACCT-007).
  Readings: `account`, or a new reason.
- Smallest fix for each: one line of section 5.39 naming the reason.
- Parks: `SessionEnded` from these three paths.

**Q5 (Tier 2, unanswered).** How `WhoMayAsync` and `GET /admin/access/admitted` answer a
word the model does not declare (AUTHZ-GATE-007, `10` section 1, LIB-API-005).

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
- Built under reading A, in process only. An organization identifier that is not a UUID
  is refused `authz.denied`, as a record with no registration is.
- Parks: `GET /admin/access/admitted`.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. These are every unit test, the commit-message gate and
  the full-history secret scan.
- The gate of record is the pipeline's run on this report's commit. The run's last
  message gives its identifier and result.
