# Round H13: a type names its data subject without an encrypted field

## 1. Items implemented

Criteria proven by tests that existed before the round, and still pass, are marked "at
base".

**`03`**

- AUTHZ-MODEL-003:
  - AC1: `AuthorizationModelTests.AUTHZ_MODEL_003_AC1_ATypeDeclaredWithoutAPurposeFailsStartup`, at base.
  - AC2: `DeclaredProcessingTests.AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor`, at base.
  - AC3:
    - `AuthorizationModelTests.AUTHZ_MODEL_003_AC3_ATypeNamingItsSubjectAloneCarriesAConsentPurpose`;
    - `ConsentGateTests.AUTHZ_MODEL_003_AC3_ATypeNamingItsSubjectAloneGatesItsConsentPurposeOnThatSubjectAsync`;
    - `TruthTableTests.AUTHZ_MODEL_003_AC3_EverySubjectConsentCaseDecidesTheWayTheTableSaysThroughTheCheckAsync`.
  - AC4: `AuthorizationModelTests.AUTHZ_MODEL_003_AC4_ASubjectAndAnEncryptedFieldNamingAnotherColumnStopTheStart`.
  - AC5: `AuthorizationModelTests.AUTHZ_MODEL_003_AC5_AConsentPurposeOnATypeNamingNoOneSubjectColumnStopsTheStart`.
  - AC6: `AuthorizationModelTests.AUTHZ_MODEL_003_AC6_FieldsNamingTwoColumnsWithNoSubjectAndNoConsentPurposeStart`.
  - Also:
    - `AuthorizationModelTests.AUTHZ_MODEL_003_ASubjectColumnNamingNoSubjectStopsTheStart`;
    - `AuthorizationModelTests.AUTHZ_MODEL_005_AC2_TheSubjectColumnIsInTheFile`, the
      serialized model carrying the column (AUTHZ-MODEL-005).
- AUTHZ-INHERIT-002:
  - AC1 to AC6: at base.
  - AC7: `ResourceRegistrationTests.AUTHZ_INHERIT_002_AC7_ATypeNamingItsSubjectIsRegisteredOnlyWithOneAsync`.

**`04`**

- PRIV-SENS-002a:
  - AC1 to AC4: at base.
  - AC5:
    - `ConsentGateTests.PRIV_SENS_002a_AC5_AWithdrawalByTheRegisteredSubjectStopsThePurposeAsync`;
    - `TruthTableTests.AUTHZ_MODEL_003_AC3_EverySubjectConsentCaseDecidesTheWayTheTableSaysThroughTheCheckAsync`,
      its withdrawn case.
- IDN-LIFE-002a AC1 (`01`), the refusal of a sensitive record naming no subject that
  D-257 item 4 restates: `ResourceRegistrationTests.IDN_LIFE_002a_AC1_ASensitiveRecordIsRegisteredOnlyForAnAccountHolderAsync`.

**`07`, `10`**

- LIB-HOST-001 AC9: `StartupValidationTests.LIB_HOST_001_AC9_ATypeNamingItsSubjectStartsAndOneWhoseFieldNamesAnotherIsRefusedAsync`.
- LIB-API-001, `ResourceTypeDeclarationBuilder<TResource>.Subject` and
  `ResourceTypeDeclaration.Subject`: `PublicAPI.Unshipped.txt` and the analysers.
- `10` section 1.5, `model.startup.declarationinvalid` and
  `model.startup.declarationmissing` as amended: the AC4, AC5 and LIB-HOST-001 AC9 tests
  above.

**Criteria verified otherwise**

- D-257 item 7, the records of processing: by review. They read a type's purposes, data
  and subject categories, and no subject column, so nothing in them changes.

## 2. Items not implemented

None.

## 3. Resolved by rule

- The registration of a sensitive record naming no subject:
  - What was out of step: `ResourceService` answered it 422 `api.request.invalid`, and
    `ResourceRegistrationTests` asserted that.
  - Governing item: IDN-LIFE-002a AC1 and AUTHZ-INHERIT-002, which refuse it 400
    `api.request.malformed` naming `subject`, as D-257 item 4 restates.
  - Rule: it is refused 400 naming `subject`; one naming a subject that does not stand
    stays 422. The batch case of
    `AUTHZ_INHERIT_002_ARefusalOnMeaningIsInvalidAndARefusedBatchWritesNothingAsync` now
    names a subject that does not stand, so it still proves a refusal on meaning.
- `BrowserProfileTests.BFF_OWN_002_AC1` (test infrastructure):
  - What was out of step: the gate lists the host fixture's domain types, and the
    fixture gains `HostCustomer`, the type naming its subject that the round's tests
    run over.
  - Governing item: BFF-OWN-002 AC1, no shipped file naming a host's domain type.
  - Rule: the list names it.

## 4. Open questions

**Q1 (Tier 2).** The conformance suite's truth table over a type that needs a subject
(LIB-TEST-001).

- What the code needs: `Janus.Conformance`'s truth table registers every record of its
  chain naming no subject. Over a type a host declares with `Subject`, the registration
  is now refused 400 `api.request.malformed` naming `subject`, and the case faults. The
  same held already for a sensitive type.
- What the spec says: AUTHZ-INHERIT-002 AC7 refuses the registration; LIB-TEST-001 does
  not say which type a host points the suite at.
- Reading A: a host points the suite at a type that needs no subject. Smallest fix: a
  sentence in LIB-TEST-001.
- Reading B: the suite registers each record whose type needs a subject under the
  case's own account. Smallest fix: that, in `TruthTable.WriteAsync`.
- Parks: nothing; the suite is unchanged.

## 5. Gate result

- Fast checks on every commit: green. These are the build with warnings as errors, the
  analysers, `dotnet format --verify-no-changes`, and the unit tests of the projects
  changed.
- Checks before the push: green. Every unit test passes (5197 in eleven projects), the commit-message gate passes
  over the round's commits, and the full-history secret scan finds nothing.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
