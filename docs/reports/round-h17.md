# Round H17: the public surface settled for the authorization part's growth

## 1. Items implemented

**`03` AUTHZ-SEAM-001 criterion 2 (D-269), 7808ec35**

- `AccessSeamTests.AUTHZ_SEAM_001_AC2_NoCallSiteBuildsAPermissionQuery`, widened. It
  scans every source file under `src/` and under `samples/HostSample/`, leaving out what
  the build writes under `bin` and `obj`. No file may name `identity.ancestry`,
  `identity.effective_grants` or `identity.consented_resources`, or name `AncestryEntry`,
  `EffectiveGrant` or `ConsentedResource`, outside the places the test lists, each with
  what it does there:
  - the gate renders the rule over them: `Janus.Authorization/Gate/`;
  - the records themselves and the sets as handed to the gate: `Janus.Core`'s
    `AncestryEntry`, `EffectiveGrant`, `ConsentedResource` and `FilterSources`;
  - kept as the library's own: `Janus.Storage/Migrations/` and
    `Janus.Storage/Authorization/`, where the closure is written with the registrations;
  - mapped: `Janus.Hosting/AuthorizationTables.cs`;
  - read for the gate: `Janus.Hosting/Authorization/RelationshipSources.cs`, the
    host's rows read on the gate's behalf;
  - counted as policed (AUTHZ-GATE-001 criterion 3): `Janus.Hosting/DeclarationCoverage.cs`
    and `Janus.Conformance/ConformanceSuite.cs`;
  - the sample host's `WorkshopContext.cs`, which maps the sets and hands them to the
    gate.
- The sample host's sets mapped over the relations are also held to that: a set is read
  only where it is declared and on the line that hands it to `FilterSources`, so a read
  through `context.Grants` elsewhere is found.
- The earlier check that `PermissionRule` is named only in a `Gate` folder of
  `Janus.Authorization` is kept in the same test.
- `AccessSeamTests.AUTHZ_SEAM_001_AC2_AReadOutsideThosePlacesIsFound`: the check finds
  four kinds of read, and passes the gate's own. The four are a relation named in SQL in
  another project, the record's set read in another project, a sample host set read in
  an endpoint, and a set read in the file that maps it. As a further check, a file added
  under `src/Janus.Identity` naming `identity.effective_grants` failed the widened test
  here, and was removed.

**`07` LIB-API-001 (D-268), LIB-API-001 and AUTHZ-SEAM-001 (D-269), LIB-TEST-001
(D-271), 7ba8684d**

- D-268: the remarks of each interface of the operations contract say that no host
  implements it, and that a member the library adds to it is an additive change. These
  are the 47 interfaces of `Janus.Core` the library implements and a host calls.
  `IMailServer` instead says a host may implement it, and a capability later asked of a
  mail server comes as an interface of its own beside it.
- The interfaces a host implements are left as they are: the extension contracts, the
  environment and callback seams of LIB-HOST-001, the assurance provider of
  LIB-HOST-004, and `IConformanceRows`.
- D-269: `AncestryEntry`, `EffectiveGrant` and `ConsentedResource` say a row is an input
  to the gate's renderings, not a permission, and what it confers is the library's to
  decide. In `AncestryEntry`, "queries it from hand-written SQL" now reads that
  hand-written SQL reaches it through the gate's SQL fragment, as D-269 amends D-017.
- D-271: `IConformanceRows<TResource>` says its members do not change, and a later
  family of cases comes with an interface of its own, which a host implements beside it.
- `CHANGELOG.md`: one line under Unreleased, `Changed`, for each of D-268, D-269, D-270
  and D-271.
- No behaviour changes. The public surface files are unchanged.

**Section 4, step 7: the release commit, 642b51a7 `chore: release 1.0.0-rc.7`**

- `CHANGELOG.md`: Unreleased becomes `## [1.0.0-rc.7] - 2026-11-06`, holding the four
  lines under `Changed`, and Unreleased is left empty.
- The public surface: no `PublicAPI.Unshipped.txt` holds a line, so nothing moved into
  the shipped files.
- The two lists are not drawn again (CONV-VCS-005, D-262). The leaked-password list of
  run 41814221201 and the Public Suffix List at version 2026-10-31_14-17-28_UTC were
  both drawn on 2026-11-04, within 90 days before the release commit. `NOTICE` keeps
  both dates.
- No migration note: a release candidate opens no major version.
- Criterion verified otherwise: the release gate over `origin/main..HEAD` says the notes
  of 1.0.0-rc.7, 1347 characters, are the release's body.

## 2. Items not implemented

None.

## 3. Resolved by rule

None.

## 4. Open questions

None.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5216 in eleven projects), and
  the commit-message, changelog, release, truth-table change, acceptance-criterion test
  name and forbidden-marker gates pass over `origin/main..HEAD`. The full-history secret
  scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
