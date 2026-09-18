# Phase 0: Skeleton

Status: stopped at open questions. The phase is incomplete.

## 1. Items implemented

None. No acceptance criterion of any phase 0 item has a passing test carrying its
identifier, because no test project can be built under the configuration `08` section
1a fixes. See Open questions 1.

The following are in place and build green, and their criteria are proven only once
that question is settled and their tests exist:

| Item | In place |
|---|---|
| CONV-LAYOUT-001 | `Janus.Core`, `Janus.Identity`, `Janus.Authentication`, `Janus.Authorization`, `Janus.Privacy`, `Janus.Storage`, `Janus.Hosting`, `Janus.Conformance`, `Janus.Cli`, `Janus.Analyzers`, under `src/`, with the dependency direction of the chapter's table and `Janus.Analyzers` on `netstandard2.0` |
| CONV-LAYOUT-002 | No public type exists in any project; the `InternalsVisibleTo` grants in the project files are exactly the permitted list |
| CONV-SETUP-001 | `Directory.Build.props` sets the framework, the language version and the seven compilation properties; only `Janus.Analyzers` overrides the framework |
| CONV-SETUP-002 | `Directory.Packages.props` carries every version; ten `packages.lock.json` files are committed; `dotnet restore --locked-mode` succeeds |
| CONV-SETUP-003 | The public surface analyser is referenced from `Directory.Build.props`; `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` exist and are empty in all ten projects |
| CONV-SETUP-004 | `.editorconfig` encodes the fixed style choices at error and sets exactly the four listed rules below error, in the scopes the table gives |
| CONV-DESIGN-008 | `Directory.Packages.props` holds exactly the identifiers of the table and no others |
| CONV-CODE-008 | JAN0001 to JAN0005 in `Janus.Analyzers`, referenced as an analyser by the nine other source projects, with release tracking files |
| CONV-DEP-001 | Lockfile per project, restore deterministic |
| CONV-DEP-002 | Dependency vulnerability alerting and automated security updates are on for the repository |
| CONV-VCS-005 | `CHANGELOG.md` at the repository root in the Keep a Changelog 1.1.0 shape, with an empty `Unreleased` section; version comes from the tag and appears in no project file |

## 2. Items in the phase not implemented

| Item | Reason | Waits on |
|---|---|---|
| Every item of section 1 above, as proven items | No test project can be built as the specification stands | Open question 1 |
| CONV-ERR-003 | No analyser rule fails the build on an empty catch | Open question 2 |
| OPS-DEP-004, and the secret-scanning row of CONV-GATE-001 | No scanner is named | Open question 3 |
| CONV-GATE-001, CONV-GATE-002 | The pipeline definitions are not written; several of their rows (acceptance-criterion test names, secret scanning, destructive-operation detection) depend on the three open questions or on items of later phases | Open questions 1 and 3; phase 1 |
| OPS-DEP-001, OPS-DEP-002 | Their criteria are stated over migrations, which the plan places in phase 1. The repository variable and the pipeline split can be written now; they cannot be exercised | Phase 1 migrations |
| OPS-DEP-003, OPS-DEP-005 | Pipeline definitions | Open question 3 |
| CONV-ERR-001, CONV-ERR-002, CONV-DESIGN-005, CONV-DESIGN-007, CONV-LOG-001 to 006, OPS-CFG-001 to 008, `10` section 4 typed settings, `10` section 4.1a policy object, LIB-API-002, LIB-API-003, LIB-PKG-001, LIB-PKG-002 | Not started. We stopped rather than write a phase of implementation whose tests cannot be run | Open question 1 |
| `Janus.Cli` commands | The bootstrap of OPS-BOOT-001 and the key rotation of OPS-SEC-003 are phase 9. The project carries its entry point and an empty command table | Phase 9 |

## 3. Resolved by rule

| Place | What was out of step | Governing item | Rule applied |
|---|---|---|---|
| The working tree handed over | The instruction files and the `docs/` tree sat one directory deeper than every path in the specification resolves to | the working guide's section 5, which places `docs/spec/`, `docs/guide/` and `docs/decision-log.md` at the repository root | The extraction wrapper directory was removed and its contents moved up one level, so that `docs/spec/00-overview.md` resolves from the repository root; no file content changed |
| `src/Janus.Analyzers` | CONV-CODE-008 does not state the accessibility of an analyser type, and the usual .NET practice is to make it public | CONV-LAYOUT-002, which permits a public type only in `Janus.Core`, `Janus.Hosting` and `Janus.Conformance` | The five analysers are `internal sealed`, which keeps the analyser project's public surface empty as CONV-SETUP-003 requires of it; we confirmed the compiler discovers and runs them by building a deliberately unsealed class and observing JAN0003 fail the build |
| `.gitignore` | the working guide's section 5 names `.gitignore` only to forbid the instruction files from appearing in it | CONV-VCS-001 and the requirement never to commit generated noise | Build output (`bin/`, `obj/`, `artifacts/`) and local editor state are excluded in a committed `.gitignore`; the instruction files and `tmp/` are excluded in `.git/info/exclude` only |

## 4. Open questions

### 1. How `08` section 1a applies to test projects (Tier 2)

**Item.** CONV-SETUP-001, CONV-SETUP-003 and CONV-SETUP-004, against CONV-TEST-007.

**What the code needs.** A test project that compiles. CONV-TEST-007 fixes test names
as `ITEM_ACn_Outcome` and `Method_Scenario_Outcome`, and the test framework CONV-TEST-007
names requires a test class and a test method to be public (its own rule xUnit1000).

**What the specification says.** CONV-SETUP-001 makes `AnalysisLevel` latest-all and
every warning an error in every project. CONV-SETUP-004 permits exactly four rules
below error and states that any further deviation is a specification defect, not a
local suppression. CONV-SETUP-003 enables public-surface tracking on every project,
with the tracking files empty in every project but `Janus.Core`, `Janus.Hosting` and
`Janus.Conformance`.

**What we measured.** A test project written exactly as CONV-TEST-007 and CONV-CODE-005
require, under exactly the configuration above, fails to build with:

- `CA1707` on every test method name, because latest-all turns the rule on and the
  names carry underscores. CA1707 is not one of the four rules CONV-SETUP-004 lists.
- `RS0016` and `RS0037` on every public test class, constructor and method, because
  public-surface tracking is on and the project's tracking files are empty.

With those two rules set aside and nothing else changed, the same project builds
clean. `CS1591` does not arise: documenting a public test type and a public test
method is what CONV-CODE-005 already requires, and we intend to document them.

**The readings we see.**

1. "Every project" in CONV-SETUP-001 and CONV-SETUP-003 means every project of
   CONV-LAYOUT-001, that is the ten source projects. Test projects then share the
   `.editorconfig` but not the public-surface tracking. Against this reading:
   CONV-SETUP-004's table places CA2007 below error for "test projects", and CA2007 is
   off unless latest-all is on, so test projects do run at latest-all and the CA1707
   collision stands under this reading too.
2. "Every project" means every project in the repository. Both collisions stand.

**The smallest fix for each.**

- CA1707: add one row to CONV-SETUP-004's table, `CA1707`, severity `none`, scope test
  projects, reason "CONV-TEST-007 fixes test names as `ITEM_ACn_Outcome` and
  `Method_Scenario_Outcome`".
- Public-surface tracking: add one sentence to CONV-SETUP-003 stating that "every
  project" is every project of CONV-LAYOUT-001, so the tracking is not enabled on test
  projects.

We have applied neither. CONV-SETUP-004 reserves this to the specification.

### 2. No analyser rule fails the build on an empty catch (Tier 3)

**Item.** CONV-ERR-003 acceptance criterion 1, against CONV-CODE-008.

**The contradiction.** CONV-ERR-003 AC1 requires an analyser rule that fails the build
on an empty catch. CONV-CODE-008 fixes the analysers this library ships as exactly
JAN0001 to JAN0005 and states that the gates naming them rely on them and on nothing
else; none of the five detects an empty catch. We built the three forms under the
configuration of `08` section 1a and measured which fail:

| Form | Result |
|---|---|
| `catch { }` | Fails, `CA1031` |
| `catch (Exception) { }` | Fails, `CA1031` |
| `catch (IOException) { }` | Builds clean |

A typed empty catch therefore passes every rule the specification permits, and
CONV-ERR-003 AC1 cannot pass as written.

### 3. The secret scanner is not named (Tier 2)

**Item.** OPS-DEP-004, and the secret-scanning row of CONV-GATE-001.

**What the code needs.** A scanner to run as a pipeline step and fail the build on
detection, on every push.

**What the specification says.** OPS-DEP-004 requires the step and names no scanner.
`08` section 10 and `19` record that the platform's own secret scanning is unavailable
on this plan, which we confirmed: enabling it on the repository is refused with
"Secret scanning is not available for this repository". D-042.3 says the step uses an
open-source scanner and names none.

**The readings we see.** Any of several open-source scanners satisfies the words.
Choosing one is adding an author trusted with repository access, which CONV-DEP-003
makes a deliberate choice, and no chapter or decision entry makes it.

**The smallest fix.** Name the scanner in OPS-DEP-004 or in a decision entry, as
CONV-DESIGN-008 names each package.

## 5. Gate result

Fast checks, run on the working tree at `bc88df9`:

| Check | Result |
|---|---|
| `dotnet build Janus.slnx` with warnings as errors and analysers at latest-all | Green |
| `dotnet format Janus.slnx --verify-no-changes` | Green |
| `dotnet restore Janus.slnx --locked-mode` | Green |
| Unit tests | Not run; no test project exists |

Full gate: not run. Integration, migration, contract and conformance suites do not
exist in this phase.

Repository, per the working guide's section 5: private repository `neolorn/janus` created,
default branch `main`, squash and rebase merges off, merge commits on, delete branch
on merge on, dependency vulnerability alerting and automated security updates on,
platform secret scanning refused by the plan as `08` section 10 records.
The instruction files and `tmp/` are excluded through
`.git/info/exclude`. Branch protection is not yet configured: the status checks
CONV-VCS-002 requires it to name are the gates of CONV-GATE-001, which open questions
1 and 3 block.

Commits on `main`: `373848a` the documentation tree, `3c3865a` the solution skeleton
and central build configuration, `bc88df9` the five analyser rules.
