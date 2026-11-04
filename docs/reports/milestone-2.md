# Milestone 2: the release, 1.0.0-rc.1

## Step 1: repository workflows

### 1. Items implemented

**Already done when the step began.** Each was checked against step 1's row, D-245 and
D-246, and left as it stands:

- `gates.yml`: a job for every gate of CONV-GATE-001, the sample host's among them
  (LIB-TEST-004). Every job runs on pull request, save secret scanning (below). The
  heavy suites run on pull request and on `main` alone (CONV-GATE-002): integration, the
  truth table, the double migration, the destructive-operation report, dependency
  vulnerability alerting and the sample host. The rest run on every push.
- `release.yml`, on `v*` tags (LIB-PKG-003, D-245), from round H8:
  - it refuses a tag that names no `X.Y.Z` or `X.Y.Z-rc.N`, and one whose version has no
    changelog section;
  - it packs the solution at the tag's MinVer version, checking that `Janus.Hosting`,
    `Janus.Conformance`, `Janus.Analyzers`, `Janus.Cli` and `Janus.Testing` are each
    packed at it;
  - it creates the tag's release with the `.nupkg` files and the version's section as
    its notes, a pre-release for `-rc.N`;
  - the workflow holds no permission, and its one job holds `contents: write` alone; it
    holds no registry credential and publishes to no registry.
- The release gate, `.github/gates/release.sh`, run by the public-surface job: a release
  commit adds one section, empties `Unreleased` and every unshipped file, follows the
  newest release, numbers a release candidate from 1, and links a migration note where a
  final release opens a major version.
- `servicing.yml` (CONV-DEP-004, R-M20, D-246), from round H8: runs daily and when
  dispatched with `contents: read` and `issues: write`. It ran on schedule on 2026-11-02,
  11-03 and 11-04, each run green, and found nothing newer than the pin 10.0.403, so it
  opened no issue.
- `leaked-passwords.yml` (AUTH-PASS-004, CONV-VCS-005, D-245), from round H8: dispatched
  by hand, it runs `tools/leaked-passwords/draw.py` on a hosted runner and keeps the list,
  dated in its first line, as the `leaked-passwords` artifact. It has never been
  dispatched.
- Dependency alerting: the repository's vulnerability alerts are on (the API answers 204),
  and the dependency vulnerability job runs on pull request and on `main`.

**Built in this step.**

- Secret scanning on pull request (CONV-GATE-001 AC1, OPS-DEP-004): 14b7714f.
  - Shown by: the pull request's run, whose secret-scanning job now runs.
- LIB-TEST-001 AC2's refusal (D-257, round H13's question 1): 0d164cd6. A truth table
  over a type whose records register with a subject, or over a type contained in one, is
  refused before anything is written, naming the type. "Sensitive" and "declaring
  `Subject`" are judged from the deployment's declaration.
  - Test: `TruthTableSubjectTests.LIB_TEST_001_AC2_ATableOverATypeWhoseRecordsNeedASubjectIsRefusedNamingItAsync`.
- D-259, a navigation return whose binding or `state` fails (BFF-ERR-001, BFF-SESS-006,
  IDN-LIFE-012): b3b03cb6. The sign-on return and a social provider's continuation
  answer 302 to the relative `/?error=auth.session.csrfinvalid`, rather than a 403 with a
  JSON body, and declare the code as one their redirect carries. The provider's callback
  still forwards to the continuation by its 303, reading no cookie.
  - BFF-SESS-006 AC6: `SignOnTests.BFF_SESS_006_AC6_AMismatchedStateIsSentToTheRootAndLoggedAsync`;
    also `SignOnTests.BFF_SESS_006_AC3_AReturnedCodeIsNotAcceptedTwiceAsync`.
  - IDN-LIFE-012 AC7: `ProviderSignInTests.IDN_LIFE_012_AC7_AProviderReturnWithAnotherStateIsSentToTheRootAsync`;
    also `ProviderSignInTests.BFF_CSRF_005a_AProviderReturnIsJudgedOnceAsync` and
    `ProviderSignInTests.IDN_LIFE_012_AReturnToLinkAfterItsSessionEndedFindsNoAttemptAsync`.
  - CONV-DESIGN-006 AC3: `EndpointDeclarationTests.CONV_DESIGN_006_AC3_TheSignOnDeclaresTheCodeItsRedirectCarries`
    and `EndpointDeclarationTests.CONV_DESIGN_006_AC3_AProviderRoundTripDeclaresTheCodesItsRedirectCarries`;
    `endpoints.txt` gains the continuation's 302.

**Criteria a pipeline run cannot decide, and how each is verified**

- A tagged commit producing every package at one version on its release (LIB-PKG-003 AC1
  and AC3, LIB-PKG-001 AC3), and the servicing workflow's issue (CONV-DEP-004 AC3) run
  only on a tag, a schedule or a dispatch.
  - Round H8 ran the release workflow's changelog step and the servicing filter by hand.
  - The servicing workflow's scheduled runs on `main` are the proof for it.
  - The tag `v1.0.0-rc.1` is the proof for the release (step 2).
- The leaked-password workflow yields its dated list only when dispatched, which this
  step has not done.

### 2. Items not implemented

None.

### 3. Resolved by rule

- `gates.yml`, the secret-scanning job (a gate that mis-implements its rule):
  - What was out of step: the job ran on push alone (dc5811ea). That commit gave two
    reasons, both about the official action, which OPS-DEP-004 no longer uses: on a pull
    request it scanned the request, not the history, and it needed a pull-requests
    permission.
  - Governing item: CONV-GATE-001 AC1, each gate run on pull request; OPS-DEP-004, the
    full history scanned by the pinned release.
  - Rule: the job runs on every event, and the script scans every branch and tag the
    checkout fetched.
- LIB-TEST-001 AC2, where the refusal is made:
  - What was out of step: the criterion has a case refused at its construction, naming
    the type, but a `TruthTableCase` names no type. The table's type is its rows' and
    the declaration's.
  - Governing item: LIB-TEST-001 AC2, refusals made before anything is written, as for a
    step-up case naming a permission bound to no gate.
  - Rule: the table is refused where its type is first known, as the run begins and
    before any connection is opened or row written.

### 4. Open questions

**Q1 (Tier 2).** How far D-259 reaches.

- What the code needed: whether the social provider's return answers as the sign-on
  return does. D-259's body names every navigation return; its title and its
  "Propagated to" named the sign-on return alone.
- Answered: every navigation return, the provider's continuation included, which the
  chapters at 007b8fde carry. Built in b3b03cb6.

**Contract file.** `endpoints.txt` lists the codes a route carries under each redirect
it answers. The provider continuation now answers two redirects, the 303 and the 302,
so each lists all its carried codes, though the 302 carries `auth.session.csrfinvalid`
alone. The file has no way to bind a carried code to one redirect; nothing is built for
it.

### 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5201 in eleven projects), the
  commit-message gate passes over the step's commits, and the full-history secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the step's last message gives.

### 6. What the next step waits on

Step 2 waits on two things:

- the lead's review of step 1 and word to make the release commit;
- the leaked-password workflow dispatched by hand, since the release commit refreshes
  the offline list and the Public Suffix List (CONV-VCS-005 AC4).

## Step 2: release 1.0.0-rc.1

### 1. Items implemented

- The release commit, 1ecbb1cc `chore: release 1.0.0-rc.1` (CONV-VCS-005, LIB-VER-001,
  CONV-SETUP-003):
  - `CHANGELOG.md`: `Unreleased` moved under `## [1.0.0-rc.1] - 2026-11-04`, its lines
    under `Added` alone, and `Unreleased` left empty. The section is the release's notes,
    as `release.yml` publishes it.
  - The public surface shipped: every `PublicAPI.Unshipped.txt` moved into its
    `PublicAPI.Shipped.txt`, each unshipped file left holding `#nullable enable` alone.
  - The offline leaked-password list (AUTH-PASS-004): the draw of run 41814221201,
    dispatched on `main`, its `leaked-passwords` artifact. It is dated 2026-11-04 and
    holds 100,000 upper-case SHA-1 hashes in ascending order. The hashes drawn are the
    same as before, and only its date line changed.
  - The Public Suffix List (AUTH-FACT-010): version 2026-10-31_14-17-28_UTC, taken from
    https://publicsuffix.org/list/public_suffix_list.dat on 2026-11-04 and carried
    unmodified. The conversion sets aside no rule of it, so the pin of
    `PublicSuffixListTests.CONV_VCS_005_AC4_ThePinMatchesTheRulesTheShippedListSetsAside`
    still holds.
  - `NOTICE`: both lists' dates.
- No migration note: a release candidate's section opens no major version (CONV-VCS-005
  AC3, D-232), and `MIGRATION.md` comes with the final 1.0.0's release commit.

**Criteria verified otherwise**

- The release gate passes over the range. Run against the release commit, the release
  workflow's changelog step finds the section and keeps it as the notes. The packages
  on the tag's release, marked as a pre-release, are the proof of the tag.

### 2. Items not implemented

None.

### 3. Resolved by rule

None.

### 4. Open questions

**Q2 (Tier 2).** Where the migration note lives, and what release notes are.

- Answered: `MIGRATION.md` at the root, one section per major version, written with the
  final 1.0.0's release commit (94bc6f33). Release candidates carry none. The release
  notes are the version's changelog section.

### 5. Gate result

- Fast checks on the release commit: green.
- The pipeline's push run on 88424102 (41822810795): red in the unit tests.
  `SearchIndexServiceTests.IDN_SRCH_003_AC7_PastTheCapacityNoIndexIsBuiltAsync` read the
  alerts once the index was withheld, and the alert is raised just after. 8c7000ad waits for
  both. The pull request's run on 88424102 (41822820169) was green.
- Checks before the push: green. Every unit test passes (5201 in eleven projects); the
  commit-message, changelog and release gates pass over the range; the full-history
  secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the step's last message gives.

### 6. What the next step waits on

The lead's tag `v1.0.0-rc.1` on the release commit, 1ecbb1cc, after the review, green
checks and the merge.
