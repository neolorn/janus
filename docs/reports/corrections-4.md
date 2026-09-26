# Corrections 4: D-166 and D-167

Status: stopped at a Tier 2 question (section 4, question 1) while applying D-166
item 114. The branch holds the corrected documentation, the offline leaked-password list
and its draw, and one Tier 1 allow-list entry. The rest of D-166 is not applied
(section 2). Before the branch was cut, pull request #6 (phase 10) was merged with the
fixes the owner asked for, and section 1 records them.

## 1. Items implemented

### On `phase-10-release`, merged as `b6d14fe` (pull request #6)

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| The changelog gate and `truth-table-change.sh` fail on output larger than the pipe buffer | `551d6ee` | CONV-VCS-005, CONV-VCS-004, CONV-DEP-002, LIB-VER-001 | No test can decide it. Verified by scenario: in a Linux container, each of the four gates was run against a scratch repository whose `git diff` output is larger than the pipe buffer. Every scenario passed with the fix. The unfixed gates were run for comparison (section 3) |
| Truth-table rows for every permission decision the phase 10 changes touch | `ce19b71` | AUTHZ-TEST-001, AUTHZ-INHERIT-002, AUTHZ-PRIN-003, CONV-DESIGN-002 AC3, AUTHZ-SCOPE-001, IDN-ACCT-007 AC2, AUTHZ-GATE-005 AC1 | `TruthTableTests.AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync`, `TruthTableTests.AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync`, `TruthTableTests.AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync`, `TruthTableTests.AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync`, `TruthTableTests.AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync` (64 cases) |
| The conformance failure, root cause | `b9fbbbf` | LIB-TEST-001, AUTH-OIDC-006 AC1 | `ConformanceSuiteTests.AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync`, `ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync` and the rest of the suite, which runs the sample host through the command |
| D-166 E.6, applied early | `e794b99` | IDN-PRIN-003, INF-BG-001, OPS-OBS-003 | `BackgroundJobsTests.IDN_PRIN_003_AC4_AnEventEveryConsumerTookIsClearedWithNobodyAskingAsync`, `BackgroundJobsTests.INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync`, `BackgroundJobsTests.OPS_OBS_003_AC1_WhatHasLapsedIsClearedWithNobodyAskingAsync` |
| REG_SESS_003, applied early | `cdc185a` | REG-SESS-003 | `RegistrationSignalsTests.REG_SESS_003_AWaitNothingSignalsEndsOnTheIntervalAsync`, `RegistrationSignalsTests.REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync` |

**Truth-table rows.**
- **Records table.** Every case is run through the check, the expression and the fragment. Three rows were added:
  - a grant on the container the record was moved into is Allowed, and one on the container it was moved out of is Denied (AUTHZ-INHERIT-002, `f5f2b08`);
  - a record of a type the model does not declare is Raised (`56fb839`).
- **Operations table.** Each case is run through the operation's own gate step; nine rows:
  - a revocation of a grant no row names, and a change to a group no row names: Denied for a caller who manages grants, Restricted for a restricted caller (`6a4488e`, IDN-ACCT-007 AC2);
  - a grant on a record no registration names: Denied;
  - a caller's own settings: Allowed, and Restricted for a restricted caller (`e0da33c`);
  - a record on a page: Allowed with the consent the page needs, ConsentRequired without it (`a86b9ba`).

The outcomes are the `Decided` enumeration: Allowed, Denied, Restricted, ConsentRequired, Raised.

**Rows not written, because D-166 reverses the decision the change made:**
- `69fa524` leaves an undeclared permission out of capabilities. D-166 entry 396 raises instead.
- `27ea1c0` rewrites `GrantStore.OnAsync` into a reverse lookup. D-166 entry 265 reverses it.
- `223eda2` refuses a consent basis for the hosting purposes at startup. D-166 supersedes it.

The rows that state the D-166 outcomes (an undeclared permission is Raised; a lookup of an unregistered record is Denied) belong with those fixes (section 2).

**Commits touched by the phase 10 changes that make no permission decision:**
- `333fc4d`: the refusal names the grant that decided it; the outcome is unchanged.
- `ebc304e`: the retention floor.
- `8a93008` and `48e405f`: the maintenance role's column privileges.
- `38268f1`: sensitive registration, a seam with no access context. D-166 entry 352 moves its codes under X5.

**Conformance, root cause.**
- SDK 10.0.303 brings `Microsoft.AspNetCore.App.Ref` 10.0.11, which ships `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.11 at the same version as the package.
- Conflict resolution keeps the framework's copy, so the package assembly is left out of the conformance test output.
- The command's runtime configuration names only `Microsoft.NETCore.App`, so the command, run from the test output, failed to load the assembly (`FileNotFoundException`).
- Under SDK 10.0.302 (reference pack 10.0.10) the package's copy wins and is copied, which is why the failure followed the SDK.
- The command's own build output holds both assemblies. The test now runs the command from there, a path written into the test assembly's metadata at build time.
- No change was made to `Janus.Cli` or its packaging.
- Proved in a Linux container on SDK 10.0.303 with the pipeline's exact `dotnet test` command: the conformance suite passed.

**REG_SESS_003, applied early.**
- Pull request run 36215901211 on `ce19b71` failed only `REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync`: a rolled-back announcement was "heard" at 0.2498 s, against the 0.25 s floor. The push run on the same commit, 36215899821, was green.
- Following the owner's instruction, the floor was not lowered and the run was not re-run.
- The wait's interval is taken on the `TimeProvider` (`Task.Delay(interval, time, ct)`). The tests now move a hand-written fake clock (`ManualTime`) that fires timers only when advanced. A test cannot hang: each wait for a timer is bounded.
- Mutation check: with the delay taken on the wall clock, both tests fail.
- The same failure occurred once before, on pull request run 36201939031 (corrections-3), attempt 1. That attempt was re-run to green before the owner's instruction; the re-run is attempt 2.

**E.6 and `cdc185a` were applied on `phase-10-release`, not on this branch.** E.6 was applied there as the owner directed. `cdc185a` was applied there because pull request run 36215901211 failed on it.

### D-167, on `corrections-3`, merged with pull request #4

| Decision | Commit | Items | Verified by |
|---|---|---|---|
| D-167 item 1, the scanner's own release at a pinned version and SHA-256, over the full history on every push | `da3ab5f` | OPS-DEP-004 | Push run 36201534531, job `Secret scanning` 108289029074: the full history was scanned with no finding |
| D-167 item 3, the entry for `PRIV-BREACH-002` in `src/Janus.Core/IAuditTrail.cs` | `d34f24a` | OPS-DEP-004 | The same job |
| D-167 item 4, the offline leaked-password list's line form | `7a4d4dd` | OPS-DEP-004 | The same job |
| The allow-list paths quoted as literal text | `3d1b5d8` | OPS-DEP-004 | Run 36201534531 failed `ProductNameTests.CONV_NAME_001_AC2_TheProductNameAppearsOnlyInNamespacesIdentifiersAndTheEntryPoint`: the escaped dots in the entries' paths left the product name followed by a backslash. Push run 36201936626 and pull request run 36201939031 were green |

### On `corrections-4`

| Instruction | Commit | Items | Tests |
|---|---|---|---|
| The corrected documentation (stash `d-167b-docs`) | `6866d9c` | none | none |
| D-166 item 114, the draw, committed under `tools/` with its checkpoints and user agent, and a test against a local fake | `d283185` | AUTH-PASS-004, CONV-VCS-005 | `test_draw.DrawTests.test_CONV_VCS_005_AC4_EveryRangeRequestNamesTheDraw`, `test_draw.DrawTests.test_AUTH_PASS_004_TheListIsTheMostPrevalentHashesInOrderUnderItsDate`, `test_draw.DrawTests.test_CONV_VCS_005_AC4_AStoppedDrawResumesFromItsCheckpoint` (`python -B -m unittest discover tools/leaked-passwords`) |
| D-166 item 114, the 100,000 most prevalent hashes in place of the old list, with the `NOTICE` paragraph | `52e4b29` | AUTH-PASS-004, INT-PWD-002 | `OfflineCorpusTests.AUTH_PASS_004_TheShippedListIsTheHundredThousandItNames`, `LibraryStructureTests.AUTH_PASS_004_TheNoticeNamesTheLeakedListsSource` |
| Secret scanning before the push | `9e6f4d3` | OPS-DEP-004 | The pinned scanner run locally, as the pipeline runs it, over every commit (section 3) |

**How and when the list was drawn.**
- **The draw.** It ran from 2026-09-25 23:54 to 2026-09-26 02:30 UTC over all 1,048,576 ranges of `https://api.pwnedpasswords.com/range/{prefix}`, with 48 workers and gzip requested.
- **Checkpoints.** Each batch of ranges was recorded as done with the kept hashes, so the draw could be stopped and resumed.
- **User agent.** `leaked-password-list-draw/2026-09-26 (drawing the 100,000 most prevalent hashes once for an offline screening list)`.
- **Selection.** The 100,000 hashes of highest count were kept; the lowest kept count is 20,990. Among equal counts the lower hash is kept, and the list is sorted.
- **Verification.** The committed file was checked to be equal to the checkpoint's kept set.
- **The terms**, read on 2026-09-26 at haveibeenpwned.com: the range API has no rate limit, and callers must send a user agent. No licence is required for Pwned Passwords, and attribution is welcomed.

## 2. Items not implemented

| Item | Reason | Waits on |
|---|---|---|
| D-166 item 114, the range requests' `User-Agent` (INT-PWD-001 AC3) | Section 4, question 1. The patch and its test are ready, and the test fails when the registration line is removed; none of it is committed | The owner's answer |
| D-166 item 114, the rest | Not reached, because the run stopped at question 1. This covers the dictionary lists (R1): the 12dicts 6.0.2 `3esl` list and the Arabic and Arabizi list, embedded, and the removal of `WordList.Directory`, `WordsFile` and the path `AddJanus` builds. It also covers the ledger line under entry 114, which is written when the paragraph is complete | Question 1 |
| D-166 R3, the dated Public Suffix List for AUTH-FACT-012 AC2 | Not reached | Question 1 |
| D-166 section C, rules X1 to X9 as sweeps | Not reached | Question 1 |
| D-166 section D, every reversal and settlement other than item 114 | Not reached | Question 1 |
| D-166 section E, every item other than E.6; E.2 is section 4, question 2 | Not reached | Question 1 |
| D-166 section F, the rows of chapter 10 | Not reached. Three unit tests fail at the head of the branch because the code still carries the codes and keys that the documentation commit renames or retires: `SettingsCatalogueTests.REF_001_AC1_EveryKeyInTheSourceIsARowOfTheReference`, `ErrorCodesTests.REF_001_AC1_EveryCodeInTheSourceIsARowOfTheReference`, `ErrorCodesTests.BFF_ERR_001_AC3_EveryCodeTheBoundaryCanAnswerIsInTheReference` | Question 1 |
| D-166 section G, the ledger lines | None written; each goes in the commit that applies its entry | Question 1 |
| Truth-table rows for D-166 entries 396 and 265 | They state the D-166 outcomes, so they belong with those fixes | Section D of D-166 |
| DR-007 AC2, a stalled restore test recorded as `unrestored` instead of overrun | A defect found in the container runs; not reached. `src/Janus.Hosting/Background/RestoreTest.cs:110` decides the overrun with a stopwatch (`elapsed > objective`), but the deadline is a timer on the same `TimeProvider`, which can fire before the stopwatch reaches the objective. The run is then recorded `unrestored`. It was seen in the container on SDK 10.0.302 and 10.0.303, not in pipeline run 36203591546. The fix is to treat the run as overrun when the deadline fired, and to move the test's timing onto a fake clock as `cdc185a` does | Question 1 |
| The full gate, the pull request for `corrections-4` | Section 5 | Question 1 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| `.github/gates/changelog.sh`, `truth-table-change.sh`, `release.sh`, `dependency-vulnerabilities.sh` (`551d6ee`) | Each piped `git` output into a search that stops at its first match. Under `pipefail`, the closed pipe failed `git` whenever the output was larger than the pipe buffer, so the gate failed on something its chapter does not say | CONV-VCS-005, CONV-VCS-004, LIB-VER-001, CONV-DEP-002; the working guide's section 3, a gate that mis-implements its own rule | Each gate reads the output whole before searching it, and checks exactly what its chapter states |
| `tests/Janus.Conformance.Tests` (`b9fbbbf`) | The test ran the command from the test project's output, which the SDK's conflict resolution leaves without an assembly the command loads | LIB-TEST-001; the working guide's section 3, test infrastructure | The test runs the command from the command's own build output. No runtime code and no shipped project changed |
| `.gitleaks.toml` (`9e6f4d3`) | The scan of the full history flagged `PRIV-BREACH-002` in `docs/decision-log.md` line 11505 (commit `6866d9c`), under the `generic-api-key` rule. It is D-167's own quotation of the comment its first entry exempts | OPS-DEP-004, D-167 item 2; the working guide's section 3, an allow-list entry for specification text | One entry: the file `docs/decision-log.md` and the exact value `PRIV-BREACH-002`, `condition = "AND"`, reason "an item identifier the decision log quotes from a documentation comment". It is an item identifier, not a credential |

## 4. Open questions

**1. Tier 2. INT-PWD-001 AC3 and D-166 item 114: where the library's version is read from.**

- **Item.** D-166 item 114: "The range requests of INT-PWD-001 carry a `User-Agent` naming
  the library and its version ... the `LeakedPasswordCorpus` client sets it once where it
  is registered." INT-PWD-001 AC3 asks for a test of it.
- **What the code needs.** The library's version at run time.
  - MinVer stamps it from the release tag into the assembly's attributes. By LIB-VER-001 and CONV-VCS-005, nothing else carries it: it appears in no project file, and nothing else sets a version number.
  - The product name is read from the root namespace, as the product-name rule already requires.
- **What the specification says.** CONV-CODE-004: "Reflection SHALL NOT be used where a type,
  a generic or a source generator serves", and AC2: "No use of `System.Reflection` outside
  the model builder and tests". This is enforced by `PublicSurfaceTests.CONV_CODE_004_AC2_NoShippedFileUsesReflection`.
  - Reading an assembly attribute is `System.Reflection`.
  - No type or generic carries the version.
  - `08` names no source generator or build step that writes a value into source.
- **Reading A.** Reading the stamped version is a use of reflection that no type,
  generic or generator serves, so CONV-CODE-004 admits it. AC2 is read with it.
  - Smallest fix: one private method in `LeakedPasswordCorpus` reads `AssemblyInformationalVersionAttribute`, and the AC2 test's exemption list gains that one file beside the model builder.
  - This is the patch in hand; its test passes.
  - Under this reading, one more point is open. The informational version carries the source revision after `+` (for example `0.0.0-alpha.0.34+52e4b29...`), so the commit identifier would go to the provider. Trimming at `+` sends the package version alone.
- **Reading B.** AC2 stands as written, and the build writes the version into source.
  - Smallest fix: one MSBuild target in `Janus.Hosting`, run after MinVer computes the version, writes a generated file holding an `internal const string` of the package version, marked as generated code (`08`, generated code).
  - `08` would need to name the pattern.
- **The patch** is kept out of history until the answer arrives.

**2. Tier 3. The "PRIV-RESTRICT-005c AC6 tension" (D-166 E.2).**

- No item of that name exists. No report, ledger entry or working note holds the original statement of the tension; only the label was carried forward. The nearest item is PRIV-RIGHT-005c, whose AC6 is the one criterion the label fits. The text puts it in tension as follows.
- **PRIV-RIGHT-005c AC6:** "No requirement obliges an application to maintain a per-schema redaction routine."
- **PRIV-RIGHT-005b** in `04` says the same in prose: "Applications no longer maintain a redaction routine. D-082 removed that requirement".
- **Against that, the same item's table** gives the host application, on `ErasureRequested`, the work: it "Redacts personal fields wherever it holds them".
- **Chapter 10's `ErasureRequested` row** reads "host-side redaction is due (PRIV-RIGHT-005b)", with "Every registered subject-event handler, **required**".
- **The code enforces the table:** a deployment with a sensitive type and no registered handler does not start (`HandlerCoverageTests` and `StartupValidationTests`, under PRIV-RIGHT-005b AC3).
- **The question.** Is a host that holds personal fields in its own tables obliged to redact them on `ErasureRequested`, or not? AC6 says no requirement obliges it; the table, chapter 10 and AC3 say one does.
- No code was changed.

## 5. Gate result

**`corrections-4`, not run.**
- The full gate was not run. At the head of the branch, the fast checks fail the three `Janus.Core` unit tests of section 2 (D-166 section F not applied), so a full gate there would not be a gate of record.
- **Fast checks, apart from those three:** green. Build with warnings as errors, analysers and `dotnet format --verify-no-changes` were run over `Janus.slnx`.
- **Local unit and contract counts at the head of the branch:**
  - `Janus.Analyzers.Tests` 20
  - `Janus.Authentication.Tests` 790
  - `Janus.Authorization.Tests` 126
  - `Janus.Cli.Tests` 14
  - `Janus.Core.Tests` 464, 3 failing
  - `Janus.Hosting.Tests` 690
  - `Janus.Identity.Tests` 89
  - `Janus.Privacy.Tests` 222
  - `Janus.Storage.Tests` 33
- **Draw tool tests:** 3, passing.
- **Secret scanning:** gitleaks 8.30.1, checked against its pinned SHA-256, was run locally over every commit of every branch and tag (738 commits). No finding after `9e6f4d3`.
- **Push and pull request.** The branch is pushed so that its commits can be read. Its pull request is not opened; it waits until D-166 is applied and the full gate is green.

**Pull request #6 (`phase-10-release`), merged as `b6d14fe`.**
- Pull request run 36217259244 on `cdc185a`: every required check green. `Secret scanning` runs on push only, and push run 36217256269 was green, with job 108335588796.
- Earlier runs:
  - 36203591546 on `f22becb`, failed: the conformance and pipe defects above.
  - 36215901211 on `ce19b71`, failed: REG_SESS_003, not re-run.
  - 36215899821, push on `ce19b71`, green.

**Pull request #4 (`corrections-3`) with D-167:**
- Push run 36201534531 failed the contract tests on `7a4d4dd`.
- Push run 36201936626 on `3d1b5d8` was green.
- Pull request run 36201939031 on `3d1b5d8` was green on attempt 2. Attempt 1 failed REG_SESS_003 alone, as above.
