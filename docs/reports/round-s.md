# Round S: the staff search, in memory

## 1. Items implemented

The project is given where two test classes share a name.

**`01` the staff search**

- IDN-SRCH-001:
  - AC1: `SearchServiceTests.IDN_SRCH_001_AC1_AnArabicSpellingVariantFindsTheNameAsync`.
  - AC2: `SearchIndexTests.IDN_SRCH_001_AC2_ATwoLetterWordMatchesOnlyANameWordItBegins`.
  - AC3: `SearchIndexTests.IDN_SRCH_001_AC3_AWordOfFourOneEditFromANameWordFindsIt`.
  - AC4:
    - `SearchServiceTests.IDN_SRCH_001_AC4_APhonesLastDigitsInArabicIndicDigitsFindItAsync`;
    - `SearchStoreTests.IDN_SRCH_001_AC4_AnUnverifiedIdentifierIsNotReadForTheSearchAsync`.
  - AC5: `SearchIndexTests.IDN_SRCH_001_AC5_EachWordMustMatchAFieldOfTheSubject`.
  - AC6: verified otherwise (below).
  - AC7: `SearchEndpointTests.IDN_SRCH_001_AC7_ASearchNamingNoLimitIsAnsweredTwentyAsync`.
  - AC8: `SearchServiceTests.IDN_SRCH_001_AC8_AResultCarriesNoContactAndANameMatchComesFirstAsync`.
  - AC9:
    - `SearchServiceTests.IDN_SRCH_001_AC9_AQueryOutsideItsBoundsIsRefusedNamingTheMemberAsync`;
    - `SearchEndpointTests.IDN_SRCH_001_AC9_AMalformedOrInvalidSearchIsRefusedNamingTheMemberAsync`.
  - AC10: verified otherwise (below).
  - AC11: `SearchServiceTests.IDN_SRCH_001_AC11_TheNormaliserIsTheIndexsAndTheQuerys`.
  - Also, in `SearchIndexTests` unless marked:
    - `Find_AFragmentOfThreeLetters_FindsTheNameContainingIt`;
    - `Find_AnArabicFragment_FindsTheNameContainingIt`;
    - `Find_APhonesLastFourDigits_FindsThePhone`;
    - `Find_AnEmailFragment_FindsTheEmailOfASubjectWithNoName`;
    - `Find_AWordTooShortOrTooFarFromANameWord_FindsNothing`;
    - `Find_ManyMatches_AreSortedByNameWithNoNameLastAndCutAtTheLimit`;
    - `Find_TwoSubjectsOfOneName_AreOrderedBySubject`;
    - `Find_NoWordOrNoLimit_Throws`;
    - `Build_ASubjectGivenTwice_Throws`;
    - `Put_ASubjectAddedOrChangedAfterTheBuild_IsFoundAsItNowStands`;
    - `Find_WhileAWriterChangesTheIndex_AnswersWhatStandsThroughoutAsync`;
    - every test of `SearchFormTests` (the normaliser).
- IDN-SRCH-002:
  - AC1: `SearchServiceTests.IDN_SRCH_002_AC1_EachPermissionAnswersItsOwnKindAloneAsync`.
  - AC2:
    - `SearchIndexTests.IDN_SRCH_002_AC2_InAnOrganizationOnlyItsMembersAndRecordsAreAnswered`;
    - `SearchStoreTests.IDN_SRCH_002_AC2_AFormerMemberIsReadInNoneOfItsFormerOrganizationsAsync`.
  - AC3:
    - `SearchServiceTests.IDN_SRCH_002_AC3_TheAdministrativeOrganizationAnswersEverySubjectButTheErasedAsync`;
    - `SearchStoreTests.IDN_SRCH_002_AC3_AnErasedOrTakenDownAccountIsNotReadAsync`.
  - AC4:
    - `SearchServiceTests.IDN_SRCH_002_AC4_ACallerWhoMayReadNoKindAskedIsRefusedAndRecordedAsync`;
    - `TruthTableTests.IDN_SRCH_002_AC4_EverySearchAdmissionDecidesTheWayTheTableSaysThroughTheAdmissionAsync`.
  - AC5: `SearchServiceTests.IDN_SRCH_002_AC5_AnOrganizationNotHeldIsNotFoundAsync`.
  - AC6: `SearchServiceTests.IDN_SRCH_002_AC6_ASystemPrincipalMayNotSearchAsync`.
  - AC7:
    - `SearchServiceTests.IDN_SRCH_002_AC7_ASearchPastTheMinutesLimitIsThrottledAsync`;
    - `SearchStoreTests.IDN_SRCH_002_AC11_TheWindowsHoldTheLaterMinuteAloneAsync`, two
      contexts counting one window.
  - AC8: `SearchServiceTests.IDN_SRCH_002_AC8_AnAdministrativeHolderSearchesAnotherOrganizationAsync`.
  - AC9: `SearchServiceTests.IDN_SRCH_002_AC9_EachResultIsCountedTowardTheReadVolumeAsync`.
  - AC10:
    - `SearchServiceTests.IDN_SRCH_002_AC10_ASearchLogsItsCountAndDurationAloneAsync`;
    - `SearchEndpointTests.CONV_LOG_003_AC4_NoLogEntryOfASearchHoldsItsQueryOrAnIndexedValueAsync`.
  - AC11:
    - `SearchStoreTests.IDN_SRCH_002_AC11_TheWindowsHoldTheLaterMinuteAloneAsync`;
    - `SubjectEraserTests.PRIV_RIGHT_005c_AC10_AnErasureDeletesTheAccountsSearchWindowsAsync`.
- IDN-SRCH-003:
  - AC1:
    - `SearchServiceTests.IDN_SRCH_003_AC1_AProcessThatServesNoSearchAnswersOffAsync`;
    - `SearchEndpointTests.IDN_SRCH_003_AC1_AProcessNotServingTheSearchAnswersOffAsync`.
  - AC2:
    - `SearchServiceTests.IDN_SRCH_003_AC2_UntilTheIndexIsBuiltTheSearchAnswersBuildingAsync`;
    - `SearchEndpointTests.IDN_SRCH_003_AC2_ASearchBeforeTheIndexIsBuiltIsAnsweredBuildingAsync`;
    - `SearchIndexServiceTests.IDN_SRCH_003_AC2_TheIndexIsBuiltOnceTheHostHasStartedAsync`.
  - AC3:
    - `SearchIndexServiceTests.IDN_SRCH_003_AC3_AChangeAnnouncedOnTheChannelIsAnsweredWithNoRestartAsync`;
    - `SearchStoreTests.IDN_SRCH_003_AC3_TheNotificationCarriesTheSubjectAloneOnCommitAsync`.
  - AC4:
    - `SearchIndexServiceTests.IDN_SRCH_003_AC4_AnErasedSubjectLeavesTheIndexOnItsNotificationAsync`;
    - `SearchStoreTests.IDN_SRCH_002_AC3_AnErasedOrTakenDownAccountIsNotReadAsync`;
    - `SearchIndexTests.Remove_ASubject_LeavesNothingOfItsTextInTheIndex`, the buffers
      zeroed; the process that erases, verified otherwise (below).
  - AC5: `SearchIndexServiceTests.IDN_SRCH_003_AC5_ALostListeningKeepsAnsweringAndRebuildsAsync`.
  - AC6:
    - `SearchIndexServiceTests.IDN_SRCH_003_AC6_HolesPastATenthAreRebuiltAsync`;
    - `SearchIndexTests.Fragmented_OnceMoreThanATenthHasLeft_IsTrue`.
  - AC7: `SearchIndexServiceTests.IDN_SRCH_003_AC7_PastTheCapacityNoIndexIsBuiltAsync`.
  - AC8:
    - `SearchIndexServiceTests.IDN_SRCH_003_AC8_UnderEnforceAFailedCheckBuildsNothingAsync`;
    - `SearchIndexServiceTests.IDN_SRCH_003_AC8_UnderWarnAFailedCheckIsLoggedAndRaisedAndTheIndexBuiltAsync`;
    - `MemoryChecksTests.Failed_EachSourceOfADiskCopy_FailsItsCheck`;
    - `MemoryChecksTests.Failed_AProcessKeptOffDisk_FailsNothing`.
  - AC9: verified otherwise (below).
  - AC10: `MemoryChecksTests.IDN_SRCH_003_AC10_ACheckWhoseSourceCannotBeReadFails`, and the
    enforce case of AC8.
  - AC11: `SliceListTests.IDN_SRCH_003_AC11_ABuiltListKeepsAHundredthSpareAndAtLeastOnePlace`;
    also `SliceListTests.Insert_AndRemove_KeepTheOrder`.
  - AC12:
    - `ProtectedConfigurationTests.IDN_SRCH_003_AC12_SettingTheMemoryChecksToWarnRaisesTheProtectedChangeAsync`;
    - `FailClosedTests.OPS_ENV_001_AC2_NoBypassFlagExistsInAnyEnvironment`, no code path
      reading the environment.

**`04`, `06`, `07`, `08`, `09`, `10`**

- PRIV-RIGHT-005c AC10: `SubjectEraserTests.PRIV_RIGHT_005c_AC10_AnErasureDeletesTheAccountsSearchWindowsAsync`.
- OPS-ALERT-005 AC4:
  - `SearchServiceTests.IDN_SRCH_002_AC9_EachResultIsCountedTowardTheReadVolumeAsync`;
  - `SearchServiceTests.OPS_ALERT_005_AC4_ARefusedSearchAddsNothingToTheReadVolumeAsync`.
- OPS-ALERT-001, the alerts `search-capacity` (Normal) and `search-unsafe` (High):
  `AlertsTests` and `VocabularyContractTests`, with the AC7 and AC8 tests above.
- LIB-HOST-001 AC8:
  - `SearchRegistrationTests.LIB_HOST_001_AC8_TheRegistrationDeclaresWhetherTheProcessServesTheSearch`;
  - `SearchIndexServiceTests.IDN_SRCH_003_AC8_UnderWarnAFailedCheckIsLoggedAndRaisedAndTheIndexBuiltAsync`.
- LIB-API-005, `ISearch`, `SearchQuery`, `SearchKinds`, `SearchResult`, `SearchMatch` and
  `SearchForm`: `PublicSurfaceTests` and `PublicAPI.Unshipped.txt`.
- CONV-LOG-003:
  - AC3: the search's carriers in `NeverLoggedTests` (`Janus.Authentication.Tests`
    and `Janus.Hosting.Tests`).
  - AC4: `SearchEndpointTests.CONV_LOG_003_AC4_NoLogEntryOfASearchHoldsItsQueryOrAnIndexedValueAsync`;
    traces and metrics verified otherwise (below).
- `09` `POST /admin/search`: `EndpointContractTests` over `endpoints.txt`,
  `SessionRequirementTests` and `error-statuses.txt` (503).
- `10` the keys of section 4.10: `configuration-keys.txt` and `commands.txt`; the code
  `identity.search.unavailable`: `ErrorCodesTests`; the table `identity.search_windows`:
  `ModelTests` and `schema.txt`; the channel `identity_search` (section 5.54): the AC3
  test above.

**Criteria verified otherwise**

- IDN-SRCH-001 AC6: by construction. A subject is indexed from `SearchSubject`, which
  holds its display name, its verified emails and phones and its organizations alone,
  and `SearchSubjectStore` reads no note, address or host field.
- IDN-SRCH-001 AC10: by construction. `SearchForm` composes and folds through the
  library's pinned Unicode tables and never through the platform's normalisation or
  casing, so the platform's Unicode version does not reach it.
- IDN-SRCH-003 AC4, the process that erases: its own search answers no erased subject
  from the commit on, since every search reads its results' names back through
  `ISearchSubjects.NamesAsync`, which answers no erased or taken-down subject; the index
  then drops it on the notification, as in every other process.
- IDN-SRCH-003 AC9: by review and by test. `identity.search_windows` holds a subject, a
  minute and a count alone (`schema.txt`); the index's text lives in pinned arrays the
  search never writes to a file; no log holds a query or an indexed value (CONV-LOG-003
  AC4 above).
- CONV-LOG-003 AC4, traces and metrics: by review. The search writes no trace attribute
  and records no metric.
- `19` INF-HOST-005 AC1 and AC2: duties of the deployment, which the library checks in
  part through the memory checks (IDN-SRCH-003 AC8).

## 2. Items not implemented

None.

## 3. Resolved by rule

- `FactorCatalogueTests.AUTH_PRIN_002_AC1` (test infrastructure):
  - What was out of step: the gate flagged `StaffSearch.cs` for its name.
  - Governing item: AUTH-PRIN-002 AC1, which forbids staff and customer as a property of
    a principal; `StaffSearch` is the registration's declaration of a feature
    (`07` LIB-HOST-001), not a property of a principal.
  - Rule: the gate's allow-list strips the name, as it does the record's origin.
- `LibraryStructureTests` (test infrastructure):
  - What was out of step: the gates of OPS-DATA-002 and OPS-DATA-003 list the listening
    connections and the store names, and knew neither the search's listener nor its
    store.
  - Governing item: IDN-SRCH-003 and `10` section 5.54, the channel `identity_search`;
    CONV-DESIGN-003, a store named for its port.
  - Rule: `SearchSignals.cs` joins the list of listeners with its comment, and the store
    is `SearchSubjectStore`.
- `PublicSurfaceTests` (test infrastructure):
  - What was out of step: CONV-DESIGN-007 AC1 expected one declaration of `AddJanus`,
    AC7 three types of another project registered by the entry point, and LIB-API-002
    AC1 knew no `StaffSearch`.
  - Governing item: `07` LIB-HOST-001's staff search row (D-256 item 13), an overload of
    `AddJanus` taking `StaffSearch` from `Janus.Hosting`; CONV-DESIGN-007 AC7, a type
    built by a factory reading a type its own project cannot reference.
  - Rule: AC1 reads one name, AC7 adds `SearchIndexHolder`, built from `StaffSearch`,
    and the mounting types add `StaffSearch`.
- `KeyMaterialTests.AUTH_KEY_002_AC4` and `BrowserCookieTests.BFF_OWN_001_AC4` (test
  infrastructure):
  - What was out of step: each read the one `AddJanus`.
  - Governing item: the same row.
  - Rule: each reads both overloads.
- `FailClosedTests.OPS_ENV_001_AC2` (a gate that mis-implements its rule):
  - What was out of step: the gate refuses any read of an environment variable, and
    IDN-SRCH-003 checks `DOTNET_EnableDiagnostics` and `DOTNET_DbgEnableMiniDump`.
  - Governing item: OPS-ENV-001, no code path reading which environment it runs in;
    the two variables are the runtime's switches, not the environment's name.
  - Rule: the gate admits exactly those two reads in `MemoryChecks` and nothing else.
- `commands.txt` (test infrastructure):
  - What was out of step: `configure`'s arguments lacked the new protected keys.
  - Governing item: `10` section 4.10, `search.maxsubjects` and `search.memorychecks`
    protected, changed by `configure` (OPS-CFG-004).
  - Rule: the command takes both.

## 4. Open questions

Q1 to Q5 are answered by D-256's amendment items 12 to 16, and Q6 by its item 17. Every
item they parked is built (section 1).

**Q1 (Tier 2).** Where the normaliser stands, and whether a host calls it.

- Answered by item 12: `SearchForm.Of`, public in `Janus.Core`; the service and the
  index in `Janus.Authentication`.

**Q2 (Tier 2).** How a process declares that it serves the search.

- Answered by item 13: an overload of `AddJanus` with `StaffSearch` (`Off`, `Served`),
  the four-parameter `AddJanus` meaning `Off`.

**Q3 (Tier 2).** Where the per-minute count is held.

- Answered by item 14: the table `identity.search_windows`, its own migration, the
  earlier windows deleted by the same statement, the rows deleted by an account's
  erasure.

**Q4 (Tier 2).** A memory check whose source cannot be read.

- Answered by item 15: it fails.

**Q5 (Tier 2).** The slice lists' spare room.

- Answered by item 16: a hundredth of the length, at least one place.

**Q6 (Tier 3).** OPS-ENV-001 against the `Development` clause of IDN-SRCH-003,
LIB-HOST-001 and `10`'s `search.memorychecks` row.

- The contradiction: OPS-ENV-001 has no code path read which environment it runs in;
  the other three had `warn` act as `enforce` outside `Development`.
- Parked: the `warn` handling.
- Answered by item 17: no environment read; under `warn` the index is built, each failed
  check logged at Warning at every start and `search-unsafe` raised naming it. Built in
  75149cf0.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5191 in eleven projects), the commit-message gate passes
  over the round's commits, and the full-history secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
