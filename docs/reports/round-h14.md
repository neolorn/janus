# Round H14: a host reads the reach of a record access session

## 1. Items implemented

None: every item of the round waits on Q1.

## 2. Items not implemented

- `21` REC-ACC-003, AC1 to AC5: parked by Q1.
- `07` LIB-API-005, `IRecordAccess`: parked by Q1, since it is the seam REC-ACC-003
  states.
- The sample host's page for a reached subject (REC-ACC-003 AC5): parked by Q1, since it
  reads the seam.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 3).** REC-ACC-003 asks a lock that only a transaction holds, and asks that
the seam join no unit of work.

- `21` REC-ACC-003 (D-261): each record is "re-read under its record's lock as
  REC-ACC-001's every action is", and `ReachedAsync` "joins no unit of work". `07`
  LIB-API-005 says the same of `IRecordAccess`: it "joins no transaction".
- In the code, a person record's lock is a row lock held until the transaction ends
  (`IPersonRecordStore.HoldAsync`, `IRecordDirectory.HoldAsync`), and taking it outside a
  transaction is a fault (`InvalidOperationException`). The library opens a transaction
  only through `IUnitOfWork.BeginAsync`, which, while the host's unit of work is open,
  joins it. So the seam cannot take the record's lock without joining a unit of work,
  and cannot join none and take the lock.
- What REC-ACC-001's actions do today: the changes of REC-ACC-002 re-read the record
  under its lock in their own unit of work (`RecordRights`). The reads take no lock:
  `GET /records/access` (`RecordAccessSessions.StateAsync`) and the reach check before
  each right (`RecordAccessSessions.ReachesAsync`) re-read the reach through
  `IRecordDirectory.InReachAsync`, outside any transaction. REC-ACC-001's sentence
  "Every action re-reads, under the record's lock" is not what these two reads do.
- Parks: every item of the round, and step 4 of the plan's section 4 (release
  1.0.0-rc.4), which comes after the round.

## 5. Gate result

- Fast checks: nothing in the code changed.
- Checks before the push: the commit-message gate passes over `origin/main..HEAD`, and
  the full-history secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
