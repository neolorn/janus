# Round H16: an organization-wide grant bound to its own organization's rows

## 1. Items implemented

**`03` AUTHZ-SCOPE-001 criterion 5 (D-265), 787711b0**

- `PermissionRule` (`Janus.Authorization`), both builders from one definition: a grant
  reaches a row only through the row's own ancestry, and the ancestry row must be
  registered in the grant's organization. An organization-wide grant matches the
  record's depth-0 row. A grant on the record or a container matches the row that names
  it as ancestor.
  - `Matching` is the expression the list filter, the page terms over the host's rows
    and the single check over the host's rows are built from.
  - `Matches` is the SQL that the fragment, the candidates of the single check and the
    page over the library's own rows are built from.
- For a grant on a record or a container the added organization condition changes no
  outcome: `ResourceService` keeps every containment tree inside one organization
  ("a tree begins and ends in one organization").
- The capability page judges each record in the organization that owns it, in one query
  whatever organizations the page spans (AUTHZ-GATE-005 AC1). The page runs a paged rule
  (`PermissionRule.Paged`), which names no organization of its own:
  - Every grant reaches a record only in the record's own organization.
  - Nothing confers in the organization where the principal holds no current membership
    (IDN-LIFE-009a), as the rule of one organization already provided.
  - Over the host's rows, what the derivations confer is read for each organization of
    the page, and a suspended one confers nothing, as `Derivations` provides.
  - A record the library holds no row for is reached by no grant, so the page no longer
    answers nothing for every record merely because its first record is unregistered.
- Tests:
  - `TruthTableTests`, two rows of `Table`, both Denied: "a grant on the whole
    organization, on a record another organization owns" and "a grant on the whole
    organization, on a row the library holds no record for". Each runs through the
    single check, the expression and the fragment (`AUTHZ_TEST_001_AC2_...`,
    `AUTHZ_PRIN_001_AC1_...`, `AUTHZ_GATE_002_AC2_...`), and through every other test
    that reads the table. Without the fix the filter and the fragment admit both.
  - `OrganizationWideGrantTests` (integration, PostgreSQL), both failing without the fix:
    - `AUTHZ_SCOPE_001_AC5_AListAdmitsOnlyTheRowsTheGrantsOrganizationOwnsAsync`: over
      a record of the grant's organization, one a second organization owns and an
      unregistered row, the filter and the fragment admit the first alone, and the
      single check admits that one and refuses the other two.
    - `AUTHZ_SCOPE_001_AC5_APageAnswersEachRecordInTheOrganizationOwningItAsync`, over
      the host's rows and over the library's own (the derivation materialised): a page
      led by the second organization's record answers that record with the second
      organization's grant (edit), the first organization's record with its own
      (read), and the unregistered row with nothing.
- Every existing case of the truth table, `GateBehaviourTests` and `ConsentGateTests`
  still passes (407 cases run here against PostgreSQL).
- The changelog line under Unreleased, under `Fixed`. No public surface change.

## 2. Items not implemented

- The release commit of section 4, step 6: it waits on the lead's word that the round's
  review passed.

## 3. Resolved by rule

None.

## 4. Open questions

**Q1 (Tier 3).** A derivation in the list filter and the fragment is not bound to the
organization the filter is asked for.

- Found by reading `PermissionRule.Derived` (both renderings), not run. The filter of
  organization A renders the derivations `Derivations.ConferringAsync` gives for A, and
  each derivation term asks only for a relationship row the principal holds on a
  container above the row. It asks nothing of the row's organization.
- The single check judges a record in the organization owning it, and there
  `ConferringAsync` gives nothing in a suspended organization. In the administrative
  organization, for a principal holding no current membership of it, the rule's subjects
  are none.
- So a filter asked for A admits, through a derivation, a record of organization B
  whose container the principal holds a relationship row on. Where B is suspended, or is
  the administrative organization the principal is no member of, the single check
  refuses that record, which AUTHZ-PRIN-001 forbids.
- D-265 names organization-wide grants alone, and says nothing of derivations. Nothing
  was changed here.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5215 in eleven projects), and
  the commit-message, changelog, release, truth-table change, acceptance-criterion test
  name and forbidden-marker gates pass over `origin/main..HEAD`. The full-history secret
  scan finds nothing. The new integration tests, the truth table, `GateBehaviourTests`
  and `ConsentGateTests` were run here against PostgreSQL, alone.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
