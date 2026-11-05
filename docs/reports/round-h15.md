# Round H15: the legal clock's instants, and a caller's, in UTC

## 1. Items implemented

**`04` PRIV-RIGHT-002 criterion 6 (D-263)**

- `WorkingCalendar` (`Janus.Privacy`): the zone still decides which day an instant falls
  on and where a day begins and ends. The end of a day (the deadline) and the start of a
  day (the warning, the escalation) are then held in UTC before anything holds or writes
  them. Every privacy request reaches the clock through `PrivacyRequestService`, so this
  covers `POST /privacy/requests`, the record's `POST /records/access/{subject}/requests`
  and `POST /account/records/{subject}/requests`, and an entry out of band.
- Tests:
  - `PrivacyRequestClockTests.PRIV_RIGHT_002_AC6_AnInAppRequestAndAnEnteredOneReadBackTheirDeadlineInUtcAsync`
    (integration, PostgreSQL, the calendar in `Africa/Cairo` on daylight saving time):
    an in-app request and an entered one are recorded, and the queue reads back
    2026-09-27T20:59:59Z and 2026-09-23T20:59:59Z, the end of the sixth working day in
    Cairo, at offset 0. Without the fix it fails on the insert with Npgsql's "only
    offset 0 (UTC) is supported".
  - `WorkingCalendarTests.PRIV_RIGHT_002_AC6_EveryInstantTheClockGivesIsHeldInUtcAsync`
    (the deadline, the warning and the escalation at offset 0, one case on daylight
    saving time and one off it)
  - `WorkingCalendarTests.PRIV_RIGHT_002_AC6_TheZoneObservesDaylightSaving`
- `WorkingCalendarTests`' earlier cases read their instants in Cairo before reading a day
  or a time of day, since the instant no longer carries the zone's offset. What they
  assert is unchanged.
- The changelog line under Unreleased, under `Fixed`. No public surface change.

**`09` API-CONV-002 criterion 5 (D-264), e6ab0a7c**

- Every service interface parameter that carries a caller's instant is converted to UTC
  at its entry, before anything holds, compares or writes it. The endpoints call these
  entries, so a body is covered the same way:
  - `IGrants.GrantAsync`, `GrantRequest.ExpiresAt` (`POST /admin/grants`);
  - `IInvitations.SetMembershipEndAsync`, `endsAt`
    (`PUT /admin/organizations/{id}/memberships/{subject}/end`);
  - `IInvitations.IssueAsync`, `InvitationRequest.GrantEndsAt`
    (`POST /admin/organizations/{id}/invitations`);
  - `IAppPasswords.CreateAsync`, `expiresAt`, handed to the mail server in UTC;
  - the signed-at instant a callback delivery carries (`CallbackSignature.SignedAt`),
    which is compared and never held.
- No other public operation takes an instant. `IMailServer.CreateAppPasswordAsync` is
  the library's own call to the mail server, and it already writes UTC. No public
  surface change.
- Tests:
  - `CallerInstantTests` (integration, PostgreSQL), each sending
    `2026-12-01T00:00:00+02:00` and reading back `2026-11-30T22:00:00Z` from the row and
    from the library's answer, at offset 0. Without the fix each fails on Npgsql's "only
    offset 0 (UTC) is supported":
    - `API_CONV_002_AC5_AGrantsEndSentAtAnOffsetIsHeldInUtcAsync`
    - `API_CONV_002_AC5_AMembershipsEndSentAtAnOffsetIsHeldInUtcAsync`
    - `API_CONV_002_AC5_AnInvitationsGrantEndSentAtAnOffsetIsHeldInUtcAsync`
  - `GrantEndpointTests.API_CONV_002_AC5_AGrantsEndSentAtAnOffsetIsHeldInUtcAsync` and
    `InvitationEndpointTests.API_CONV_002_AC5_AnEndSentAtAnOffsetIsSetInUtcAsync` (the
    JSON body at +02:00 through the endpoint)
  - `AppPasswordsTests.API_CONV_002_AC5_AnAppPasswordsEndSentAtAnOffsetIsHeldInUtcAsync`
- Criterion verified otherwise: an app password's end is held by the mail server, not
  by PostgreSQL. The test above shows the server is handed it in UTC and lists it in UTC.
  `JmapMailServerTests.INT_MAIL_010_AC1_AnAppPasswordIsOneCallOnTheMailboxsAccountAsync`
  shows it is written in UTC.
- The changelog line under Unreleased, under `Fixed`.

## 2. Items not implemented

None.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 2).** Instants a caller gives with an offset other than 0.

- Found: no other place in the library builds a `DateTimeOffset` from a zone's offset.
  `ReadVolume` reads the zone for a calendar date alone, and the mail server's instants
  are parsed to UTC. But an instant a caller gives is passed on as given: `endsAt`
  (`PUT /admin/organizations/{id}/memberships/{subject}/end`), `expiresAt`
  (`POST /admin/grants`, an app password), `grantEndsAt` (an invitation), and the
  operations contract's `DateTimeOffset` parameters. A body such as
  `"2026-12-01T00:00:00+02:00"` would then meet the same Npgsql refusal and answer 500.
  We read this from the code and did not run it.
- What the specification says: `09` states no form for an instant in a request body,
  and D-263 names the legal clock alone.
- Readings:
  - **A:** an instant is held in UTC at the boundary, as D-263 has the clock do.
    Smallest fix: every instant taken from a body or a contract parameter is converted
    to UTC before anything holds it.
  - **B:** a body instant at an offset other than 0 is refused with
    `api.request.malformed`, naming its member. Smallest fix: the check in each body's
    reading, with a sentence in `09`.
- Parked: nothing of this round. Nothing was changed here.
- Answered: D-264 (94ea9724), reading A. API-CONV-002 gains criterion 5, and the round
  is widened to it. Built (section 1). Running the tests confirmed the 500 we had read
  from the code.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes, and the commit-message,
  changelog, release, acceptance-criterion test name and forbidden-marker gates pass
  over `origin/main..HEAD`. The full-history secret scan finds nothing. The new
  integration tests were run here against PostgreSQL, alone.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
