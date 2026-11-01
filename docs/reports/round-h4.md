# Round H4: what a host's integration found

## 1. Items implemented

Items are D-232 to D-235, with D-234's amendments as the decision log reads at
507ba258. Criteria proven by tests that existed before the round, and still pass, are
marked "at base".

- D-232, release candidates (LIB-VER-001, LIB-TEST-002, CONV-VCS-003, CONV-VCS-005,
  OPS-MIG-005):
  - `.github/gates/release.sh` takes `X.Y.Z-rc.N` versions and `vX.Y.Z-rc.N` tags,
    orders them by SemVer precedence, numbers a version's candidates from 1 without a
    gap, and refuses a candidate after its final release.
  - The version part, the contract lists and the breaking marker are judged against the
    newest final release, never a candidate; before the first final release nothing
    binds.
  - A candidate's section asks for no link to a migration note; a final major release
    still does.
  - `.github/gates/double-migration.sh` picks the previous release with candidates
    sorted before their final release, so a candidate's database is the one upgraded
    (OPS-MIG-005 criterion 3).
  - MinVer reads `vX.Y.Z-rc.N` as it is, with the tag prefix already set in
    `Directory.Build.props`: no change. No release workflow exists yet (Milestone 2
    step 1), so none is changed.
  - No tag is made.
- D-233, a pressed link's answer naming its challenge: not implemented (section 2).
- D-234, an enrolment session replaces a lost mailbox in any mode (REG-IDENT-007,
  AUTH-RECOV-002, AUTH-STEP-007, IDN-LIFE-008):
  - `IdentifierServiceTests.REG_IDENT_007_AC12_AnEnrolmentSessionReplacesALostAddressInAnyModeAsync`
    (taken at `identifiers.email.max` 10, the new address in the old one's place,
    primary included; the same replace on an account session refused
    `identity.identifier.invalid`);
  - `IdentifierServiceTests.REG_IDENT_007_AC12_AnEnrolmentSessionTakesOneReplaceAsync`
    (a replace after the swap refused `authz.denied`; the session stays open, IDN-LIFE-008);
  - `IdentifierServiceTests.REG_IDENT_007_AC12_AnEnrolmentSessionHoldsOnePendingReplaceAsync`
    (another identifier while one is pending refused `identity.change.pending`; the
    repeat stays a resend);
  - `IdentifierServiceTests.REG_IDENT_007_AC12_AnAbandonedReplaceFreesTheEnrolmentSessionAsync`;
  - `IdentifierServiceTests.REG_IDENT_007_AC12_AnEnrolmentSessionReplacesNoPhoneAsync`
    (the session replaces an email its enrolment lists, and nothing else);
  - `RecoveryLinkStoreTests.REG_IDENT_007_AC12_ThatASessionsReplaceAppliedIsKeptOnItsLinkAsync`
    (integration; the count is kept on the session's link, migration
    `20261101002929_CountAnEnrolmentReplace`);
  - `CredentialFlowTests.AUTH_RECOV_002_AC8_TheEnrolmentSessionReplacesTheLostAddressInAnyModeAsync`
    (over HTTP: the replace, its verification, the second replace refused 403
    `authz.denied`, and add and remove answered as no session there);
  - `CredentialServiceTests.AUTH_STEP_007_TheFactorAnEnrolmentSessionEnrolsIsNotGatedAsync`;
  - the password the session sets, ungated: `CredentialServiceTests.AUTH_RECOV_002_TheEnrolmentSessionSetsAPasswordAndThenEndsAsync`
    (at base).
- D-235, a stored policy reads past a retired step-up action (AUTH-STEP-001, `10`
  section 5a, OPS-MIG-001):
  - `SettingWrittenFormTests.AUTH_STEP_001_AStoredPolicyReadsPastARetiredAction`;
  - `SettingWrittenFormTests.AUTH_STEP_001_AStoredOverrideReadsPastARetiredAction`;
  - `SettingWrittenFormTests.AUTH_STEP_001_AStoredGateNamingNoActionIsRefused`;
  - `SettingWrittenFormTests.AUTH_STEP_001_AnEnteredRetiredActionIsRefusedAsAMisspelledOne`;
  - `StartupValidationTests.AUTH_STEP_001_ADeploymentWhosePoliciesNameARetiredActionStartsAsync`
    (integration; the host starts, every current action gated as before, the stored
    rows unchanged by the read);
  - `ConfigurationStoreTests.AUTH_STEP_001_TheMigrationRemovesTheGatesOfARetiredActionAsync`
    (integration; migration `20261101000056_RemoveTheGatesOfARetiredAction`).
  - The log line at the read: not implemented (section 2).

Criteria a test cannot decide, and how each was verified:

- LIB-VER-001 AC3, CONV-VCS-003 AC2 and CONV-VCS-005 (a candidate's section): the
  gates are shell scripts the pipeline runs over the repository's own history, which
  holds no release. Verified by seven scenarios built as scratch repositories under
  `tmp/` and run through `release.sh`: a candidate removing a member before the first
  final release passes, the same removal in a minor release after it fails, a candidate
  of a minor is held to the minor, a candidate of the next major passes, candidate
  numbering and order, candidate tags, unmarked removals before and after the first
  final release, and a contract list's entry taken away before and after it. Each gave
  the expected result; the gate before the round fails the first.
- OPS-MIG-005 criterion 3: by the same scenarios, the previous release
  `double-migration.sh` picks is the newest candidate before its final release, and the
  final release once it exists.
- No table, export or audit field holds a step-up action's number (D-235): verified by
  reading. `schema.txt` holds no integer column for an action; the audit record's
  `action` is the audit action's text; policies, overrides, their exports and alerts
  write an action by its written name; the storage project never refers to
  `StepUpAction`. Nothing to fix.

## 2. Items not implemented

- D-233: parked on Q19.
- D-235, the Information log line once per policy at the read: parked on Q20. The drop
  at the read, the write refusal and the migration are built.

## 3. Resolved by rule

None.

## 4. Open questions

**Q19 (Tier 3), open.** D-233 asks a pressed link's `factorRequired` or
`deviceVerificationRequired` answer to name its challenge in `challengeId`.

- What the code needs: the handle of the challenge the link completed its factor on,
  at the press.
- What the spec says: `09` `POST /auth/factor`, `challengeId` is the handle the sign-in
  began with; D-233, the press answers it.
- What the code holds: `signin_challenges` is keyed by the handle's 32-byte fingerprint
  (`pk_signin_challenges`, `ck_signin_challenges_handle`), and `signin_links.challenge`
  holds that fingerprint. The handle itself is stored nowhere, and the press carries the
  link's token alone (D-224), so the press cannot answer the handle.
- Touches security semantics (a bearer handle of a sign-in in progress, how it is
  stored and where it is answered), so no reading is proposed.
- Parks: D-233 whole (`09` `POST /auth/factor` and the `POST /auth/link` paragraph,
  `10` section 5's member, `18` the landing page's flow), and the plan's first proof.

**Q20 (Tier 2), open.** Where D-235's log line comes from.

- What the code needs: a log entry at Information, once per policy, where a stored
  policy or override drops a gate naming a retired action.
- What the spec says: `10` section 5a, the read logs it once per policy at Information.
- What the code holds: the read is the setting's stored form in `Janus.Core`, reached
  through the configuration store of `Janus.Storage`. The library logs in
  `Janus.Hosting` alone, through internal contracts an area declares; the store is also
  built by every command of the command-line tool, which holds no logger.
- Reading A: an internal contract the store calls and the host implements; the tool's
  reads log nothing. Smallest fix: a sentence in `10` section 5a.
- Reading B: as A, and the tool writes a line to its error stream. Smallest fix: a
  sentence in `10` section 5a and in the tool's chapter.
- Reading C: the host scans the stored policies once at startup and logs each that
  names a retired action; reads stay silent. Smallest fix: a sentence in `10` section 5a.
- Parks: the log line alone.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit and contract test passes (5183 in eleven
  projects), the changelog, acceptance-test-name and forbidden-marker gates and
  `release.sh` pass over the round's commits, and the full-history secret scan finds
  nothing. The commit-message gate passes over every commit of ours and fails on three
  of the documents' commits merged from `round-h4-docs`, which are pushed and not ours
  to amend: 01df0c7a, c48fcd85 and 5fdcec31, descriptions of 75, 76 and 78 characters,
  over CONV-VCS-003's 72. The pipeline's commit-message job reads the same range.
- Every integration test the round added runs first in the pipeline.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
