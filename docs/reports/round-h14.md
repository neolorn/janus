# Round H14: a host reads the reach of a record access session

## 1. Items implemented

**`21` REC-ACC-003 and `07` LIB-API-005: `IRecordAccess` (D-261)**

- `IRecordAccess.ReachedAsync` (`Janus.Core`), mapped by `Janus.Hosting` to the record
  access session the request's browser holds through the pre-authentication session, as
  the `/records/access` endpoints read it. The reach is re-read as `GET /records/access`
  re-reads it, through `IRecordDirectory.InReachAsync`, with no lock and outside any
  transaction, and answered in subject order. No endpoint, no access context, no unit of
  work, no audit.
- Tests, in `RecordReachTests`:
  - `REC_ACC_003_AC1_TheSubjectsTheSessionReachesAreAnsweredInOrderAsync`
  - `REC_ACC_003_AC1_ASessionReachingNothingAnswersAnEmptyListAsync`
  - `REC_ACC_003_AC2_ASessionThatHasEndedIsRefusedAsync` (another browser, ended, past
    its lifetime)
  - `REC_ACC_003_ABrowserSignedInHoldsNoSessionAsync`
  - `REC_ACC_003_AC3_ARecordAttachedAfterTheSessionOpenedIsNotAnsweredAsync`
  - `REC_ACC_003_AC4_OutsideABrowsersRequestTheReadIsAFaultAsync`
- The public surface: `IRecordAccess` and `ReachedAsync` in `Janus.Core`'s unshipped
  file. The changelog line under Unreleased.

**REC-ACC-003 AC5: the sample host (LIB-TEST-004)**

- `GET /walk-ins/{ticket}` shows a guest's own repair in the browser that reached the
  guest's record by a code, and answers another guest's repair as one that does not
  exist, 404 in the same bytes.
- Test: `GuestTests.REC_ACC_003_AC5_AGuestSeesTheirOwnRepairAndNoOtherAsync`.

## 2. Items not implemented

- The release commit of section 4, step 4, `chore: release 1.0.0-rc.4`: parked by Q2.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 3).** REC-ACC-003 asked a lock that only a transaction holds, and asked that
the seam join no unit of work.

- Asked: `21` REC-ACC-003 and D-261 had each record "re-read under its record's lock",
  and `ReachedAsync` join no unit of work. A person record's lock is a row lock held to
  the transaction's end, taken only inside one, and the library opens a transaction only
  through `IUnitOfWork.BeginAsync`, which joins the host's. REC-ACC-001's own reads
  (`GET /records/access`, the check before each right) take no lock.
- Parked: every item of the round.
- Answered: D-261 amended (cdf7652c). `ReachedAsync` re-reads the reach as the session's
  state read does, with no lock, a read of that moment; REC-ACC-001's sentence now says a
  change re-reads under the record's lock and a read without one. The round is built from
  the chapters as they now read.

**Q2 (Tier 2).** Whether 1.0.0-rc.4 draws the two lists again.

- Asked: CONV-VCS-005 has each release refresh the offline leaked-password list and the
  Public Suffix List, and its AC4 has them drawn for that release. Both were drawn on
  2026-11-04 for 1.0.0-rc.1 (the leaked-password list of run 41814221201, the Public
  Suffix List at version 2026-10-31_14-17-28_UTC). 1.0.0-rc.2 and 1.0.0-rc.3 carried
  them as they were, the reason given being that both were drawn that day. 1.0.0-rc.4 is
  made on 2026-11-05, so that reason no longer holds.
- Readings:
  - **A:** every release candidate draws both lists again. Smallest fix: the
    leaked-password workflow dispatched by hand on `main` and its artifact taken, the
    Public Suffix List taken again, the pin of the suffix rules set aside reviewed, and
    `NOTICE`'s two dates, all in the release commit.
  - **B:** a release candidate carries the lists of the candidate before it, as
    1.0.0-rc.2 and 1.0.0-rc.3 did, and the final 1.0.0 draws them. Smallest fix: the
    release commit moves `Unreleased` and the unshipped surface alone.
- Parked: the release commit. A release commit pushed under the wrong reading would need
  a 1.0.0-rc.5, since the tag must stand on the commit that adds the version.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5209 in eleven projects)
  and every contract test (214); the commit-message, changelog, release,
  truth-table change, acceptance-criterion test name and forbidden-marker gates pass
  over `origin/main..HEAD`; the full-history secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
