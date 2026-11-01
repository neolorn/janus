# Round H8: Milestone 2's known issues, and what its map found

## 1. Items implemented

- D-247, the roles a giver may hand out (AUTHZ-GRANT-005, LIB-API-005, `09`
  `GET /admin/grants/assignable`):
  - `IGrants.AssignableAsync` and `GET /admin/grants/assignable?organization={id}` answer
    the roles a grant by the caller in that organization would admit, each with
    `grantsMustEnd`, sorted by name; no step-up; 403 `authz.denied` without
    `grant:manage` there, recorded as any refused request; 404
    `identity.organization.notfound`; 400 `api.request.malformed` without
    `organization`; an admitted read records nothing;
  - `GrantEndpointTests.AUTHZ_GRANT_005_TheRolesAGiverMayHandOutAreReadAsync`;
  - `HandOutTests.AUTHZ_GRANT_005_TheRolesAGiverMayHandOutAreTheOnesAGrantAdmitsAsync`
    (integration);
  - `TruthTableTests`: the rows "the roles the caller may hand out read, the role its
    role lists among them and no other, by a caller managing grants" and "the roles the
    caller may hand out read, by a caller managing no grants".
- D-242, registration carries a pending authorization (AUTH-SESS-012 AC11, REG-SESS-008
  AC4, FE-REG-005):
  - the terms step carries the pending authorization to the session it establishes and
    answers 201 `{ continueTo }`, or an empty 201 where nothing is carried;
    `GET /auth/session` answers `continueTo` beside `landing` on every session bound to
    the record while it lives;
  - `ContinuationTests.AUTH_SESS_012_AC11_ARegistrationThatCompletesIsSentBackWithTheCodeAsync`;
  - `ContinuationTests.AUTH_SESS_012_AC11_ARegistrationPastTheContinuationLifetimeLeavesTheLandingAsync`.
- D-241, the provider's documents on the machine profile (BFF-MACH-001, BFF-CSRF-005a,
  `09` section 9, `10` section 4.5):
  - `/.well-known/openid-configuration` and `/oidc/jwks` are machine-governed and in the
    cookie-ignored set: a fetch is no first contact and opens, reads or writes no
    pre-authentication session;
  - `oidc.discovery.ratelimit` and `oidc.jwks.ratelimit`, 60 a minute per source, fixed
    window, counted in the instance's memory so a fetch writes nothing; over the limit,
    429 with `Retry-After` and no body;
  - `OidcFlowTests.BFF_CSRF_005a_AC5_AFetchOfTheProvidersDocumentsIsNoFirstContactAsync`
    (both routes, with no cookie and with a session cookie);
  - `OidcFlowTests.BFF_MACH_001_AC4_PastItsLimitAFetchIsAnsweredTooManyAsync`;
  - `SettingsCatalogueTests.LIB_API_001_AC2_TheKeysAreTheContract` with the two keys.
- D-243, `signIn` (AUTH-STEP-002 AC11, AUTH-STEP-005, AUTH-STEP-008, AUTH-FACT-002
  criterion 7, `09` `POST /auth/step-up`, `10` `auth.stepup.required`):
  - step 3 answers `signIn` where the gate asks `delegated`, the session has not reached
    it since its last downgrade and every usable factor of the account is social; the
    refusal carries `outcome` `signIn`, no option and a null `pendingUntil`;
  - `StepUpTests.AUTH_STEP_002_AC11_ADowngradedSocialOnlySessionIsToldToSignInAgain`
    (one provider and two), which also shows the session a new sign-in establishes
    passing the gate;
  - `StepUpTests.AUTH_STEP_002_AC11_AnAccountHoldingAFactorOfItsOwnIsNotToldToSignInAgain`;
  - `StepUpRefusalTests.AUTH_STEP_002_AC11_TheRefusalThatSendsThePersonToSignInAgainSaysSignIn`;
  - `AuthenticationServiceTests.AUTH_STEP_002_AC11_ASocialCredentialPresentedAtAStepUpIsStillRefusedAsync`;
  - the case where a loss report is pending is open (section 4, question 1).
- D-244, a passkey sign-in opened without an identifier (REG-IDENT-003 AC3,
  AUTH-ABUSE-001 AC15, `09` `POST /auth/begin`):
  - `identifier` is optional on `POST /auth/begin` and nullable on
    `IAuthentication.BeginAsync`; absent, the begin opens a challenge naming no account
    and no identifier, bound to the pre-authentication session, and answers
    `available` `["passkey"]`; an empty identifier is still malformed;
  - the passkey's user handle names the account at `POST /auth/factor`, the challenge
    then names it; an assertion with no user handle is refused;
  - each such begin ends the challenges the same pre-authentication session opened
    without an identifier that no passkey has named; two begins of one browser that
    run at the same instant can each leave one, which their lifetime ends;
  - the delay is asked against the source before the challenge opens, 429
    `auth.throttled` with `retryAt`; a refused assertion counts against the source, and
    against the account its user handle names where an account has that identifier,
    under no identifier;
  - `AuthenticationServiceTests.REG_IDENT_003_AC3_ABeginWithoutAnIdentifierOffersThePasskeyAloneAsync`;
  - `AuthenticationServiceTests.REG_IDENT_003_AC3_APasskeyAgainstABeginWithoutAnIdentifierSignsInTheAccountItsHandleNamesAsync`;
  - `AuthenticationServiceTests.REG_IDENT_003_AC3_AnAssertionWithNoUserHandleIsRefusedAsync`;
  - `AuthenticationServiceTests.REG_IDENT_003_AC3_ASecondBeginWithoutAnIdentifierReplacesTheFirstsChallengeAsync`;
  - `AuthenticationServiceTests.AUTH_ABUSE_001_AC15_ARefusedAssertionCountsAgainstTheSourceAndTheAccountItsHandleNamesAsync`;
  - `AuthenticationServiceTests.AUTH_ABUSE_001_AC15_ABeginWithoutAnIdentifierFromADelayedSourceOpensNoChallengeAsync`;
  - `PasskeyFlowTests.REG_IDENT_003_AC3_APasskeySignsInFromABeginWithoutAnIdentifierOverTheWireAsync`;
  - `PasskeyFlowTests.REG_IDENT_003_AnEmptyIdentifierIsMalformedAsync`;
  - `ChallengeStoreTests.REG_IDENT_003_AC3_ABrowsersUnnamedSignInsEndTogetherAndANamedOneIsCarriedAsync`
    (integration);
  - `ApplicationJsonTests.CONV_DESIGN_007_AC9_AMemberSpelledInAnotherCaseIsNotReadAsync`
    showed the miscased member unread by the 400 a begin without `identifier` answered;
    it now shows it by the passkey-only answer such a begin gives.
- D-245, the release workflow (LIB-PKG-003, plan section 4 steps 1 and 2):
  - `.github/workflows/release.yml` on `v*` tags: refuses a tag that is no
    `X.Y.Z` or `X.Y.Z-rc.N`, and one whose version has no changelog section; packs the
    solution at the tag's MinVer version and checks that `Janus.Hosting`,
    `Janus.Conformance`, `Janus.Analyzers`, `Janus.Cli` and `Janus.Testing` are each
    packed at it; creates the tag's release with the `.nupkg` files and the version's
    changelog section as its notes, a pre-release for `-rc.N`; the workflow holds no
    permission, its one job `contents: write` alone, and no registry credential;
  - `.github/workflows/leaked-passwords.yml`, dispatched by hand: runs
    `tools/leaked-passwords/draw.py` on a hosted runner and keeps the dated list as the
    `leaked-passwords` artifact.
- D-246, servicing the SDK (CONV-DEP-004 AC3, R-M20):
  - `.github/workflows/servicing.yml`, daily and when dispatched, `issues: write` alone:
    reads the channel's `releases.json` at Microsoft's release metadata location,
    takes the newest SDK newer than the pin in the pin's feature band, and opens one
    issue titled "Servicing the SDK: X is out", or updates the open one, saying whether
    the releases since the pin carry security fixes and naming their CVEs; it writes no
    commit and opens no pull request.

Criteria a test cannot decide, and how each was verified:

- LIB-PKG-003 AC1 and AC3, LIB-PKG-001 AC3, plan section 4 step 1's release and
  leaked-password conditions, and CONV-DEP-004 AC3 run only on a tag, a schedule or a
  dispatch, which no pipeline run of a branch makes; the permissions and the absence
  of a registry credential are read from the files. The changelog step of
  `release.yml` was run against a sample changelog: `v1.0.0-rc.1` and `v0.9.0` with
  sections passed and kept their sections as notes, `v1.0.0-rc.2` and `v1.0.0` without
  were refused, and `v1x0.0` was refused as no version. The filter of
  `servicing.yml` was run against the live 10.0 metadata: the pin 10.0.403 found
  nothing newer, 10.0.401 found 10.0.403 with security fixes, 10.0.112 found 10.0.114,
  and 10.0.100 found 10.0.114. Their first runs on `main` are the proof.
- FE-PM-004 criteria 1 to 3 and FE-REG-005's done step are the frontends' (plan
  section 4, D-220); the server side each needs has the tests above.

## 2. Items not implemented

None, save the case of D-243 that question 1 parks.

## 3. Resolved by rule

- `src/Janus.Authentication/Factors/StepUpOffer.cs`: at a `delegated` gate the offer
  listed a social factor as a combination, so a downgraded social-only session was
  answered `present` with `[google]`, not `report-loss` as plan section 4's known issue
  says. AUTH-STEP-005 AC1 states that a social credential is never offered, and D-243
  builds on that. Rule: a combination holding a factor only a provider asserts is
  never offered at a step-up (AUTH-STEP-005).
- `tests/Janus.Hosting.Tests/Bff/BrowserCookieTests.cs` and `BrowserProfileTests.cs`:
  the two guard tests that list the boundary files reading a configuration key list
  `FetchRateLimiting.cs` beside `SourceRateLimiting.cs`, as the limit of D-241 reads
  its keys there and writes no cookie. Rule: test infrastructure, the least change that
  lets BFF-MACH-001 AC4's test exist.
- `docs/guide/implementation-plan.md` section 4's first known issue says step 3
  answered `report-loss`; the code answered `present` (above). A defect in the text,
  left to the lead.

## 4. Open questions

1. Tier 3, AUTH-STEP-002 step 3 (D-243), parks one case of `signIn`. An account whose
   only usable factors are social while a loss report on another of its factors is
   pending (that factor suspended, so not usable) meets both "every usable factor of
   the account is social → `signIn`" and "a loss report is already pending → say so,
   the gate stays closed until invalidation completes". It arises where the gate asks
   `delegated`, such as enrolling a social factor on a downgraded session. The two
   bullets give different answers and no text orders them. Built: `signIn` where no
   loss report is pending; with one pending the answer stays `pending`, as before this
   round.
2. Tier 2, CONV-DEP-004 (D-246): the servicing workflow holds `issues: write` and
   nothing else, so it reads `global.json` with a token that holds no `contents`
   permission. That reads a public repository and not a private one. Built as stated,
   for the repository as it is now.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: 5226, green.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
