# Round H11: a recovery's approver, and a browser application without a secret

## 1. Items implemented

- D-251, a recovery's approver reaches every role the account holds (AUTH-RECOV-002,
  AUTHZ-GRANT-005, `09` the approval route's 403, `10` section 1.3):
  - an approval is refused 403 `authz.role.notassignable` unless the approver may hand
    out every role the account holds through a live allow grant, its own or a group's,
    directly or through nesting, each judged in the organization of that grant as
    AUTHZ-GRANT-005 judges a grant there; an account holding no role is approved as
    before;
  - judged after the admission of `recovery:approve` and before the reason and the
    step-up, and again when the approval stands; a refusal writes nothing and is
    recorded as a refusal; where more than one approver is required, each is judged
    alone;
  - `RecoveryServiceTests.AUTH_RECOV_002_AC9_AnApproverWhoMayNotHandOutTheAccountsRolesIsRefusedAsync`;
  - `HandOutTests.AUTHZ_GRANT_005_AC11_ARecoverysApprovalReachesOnlyTheRolesTheApproverHandsOutAsync`;
  - `RecoveryFlowTests.AUTH_RECOV_002_AC9_AnApproverBeyondTheAccountsRolesIsAnsweredNotAssignableAsync`.
- D-252, the `public-application` client kind (AUTH-OIDC-001, AUTH-OIDC-004,
  AUTH-OIDC-006, AUTH-KEY-001, OPS-BOOT-001, OPS-SEC-002, INT-MAIL-004, `09`, `10`
  sections 4 and 5.13):
  - registered from the server alone with every return address and an optional
    audience, and no secret drawn; further addresses and an audience are refused for
    any other kind, and every address is refused as startup refuses it:
    `ClientRegistryTests.AUTH_OIDC_001_AC6_APublicApplicationIsRegisteredWithNoSecretAsync`,
    `ClientRegistryTests.AUTH_OIDC_001_FurtherAddressesAndAnAudienceAreAPublicApplicationsAloneAsync`,
    `ClientRegistryTests.AUTH_OIDC_001_AClientIsNotMovedAcrossTheSecretLineAsync`,
    `RegisterClientTests.OPS_BOOT_001_AC13_APublicApplicationIsRegisteredWithEveryAddressAndNoSecretAsync`,
    `RegisterClientTests.AUTH_OIDC_001_AFurtherAddressForAnotherKindIsRefusedAsync`;
  - the registry's table holds the kind, the further addresses and the audience, with
    checks that a public application alone holds no secret and alone holds either:
    migration `20261101110409_AddPublicApplicationClients`, `schema.txt`,
    `ModelTests.REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames`;
  - a direct `GET /oidc/authorize` from a public application with PKCE `S256` is
    answered, and one from any other kind is still refused `invalid_request`; the plain
    method, a challenge naming no method, no challenge and `offline_access` are refused
    `invalid_request`, an unregistered scope `invalid_scope`; a push without client
    authentication is taken, naming any registered address:
    `PublicApplicationTests.AUTH_OIDC_006_AC5_ADirectRequestFromAPublicApplicationIsAnsweredAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_AC5_ADirectRequestFromAnyOtherKindIsRefusedAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_AC5_APublicRequestWithoutS256OrAskingToHoldOneIsRefusedAsync`,
    `PublicApplicationTests.AUTH_OIDC_001_AC5_APublicApplicationsUnregisteredScopeIsRefusedAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_APublicApplicationPushesWithoutAuthenticatingAsync`;
  - a token request that presents a secret is refused `invalid_client`; the ID token's
    `aud` is the client, the access token's its audience or, with none registered, the
    client itself, its scopes `openid` and the registered scopes asked for, and no
    refresh token is issued:
    `PublicApplicationTests.AUTH_OIDC_006_AC5_ATokenRequestFromAPublicApplicationWithASecretIsRefusedAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_APublicApplicationWithNoAudienceIsItsOwnAsync`,
    and the first test above;
  - the access token lives `oidc.publicapplication.accesstoken.lifetime`:
    `OidcServiceTests.AUTH_OIDC_004_AC2_APublicApplicationsTokenLivesItsOwnLifetimeAsync`,
    `PublicApplicationTests.AUTH_OIDC_004_AC2_APublicApplicationsTokenLivesItsOwnLifetimeAsync`,
    and the settings catalogue's contract;
  - a signing key's overlap follows the greater of the two lifetimes:
    `SigningKeysTests.AUTH_KEY_001_AC9_APublicApplicationsLongerLifetimeLengthensTheOverlapAsync`;
    the tests that counted the default overlap now count 20 minutes, the public
    lifetime's default of 15 being the greater;
  - discovery advertises `require_pushed_authorization_requests: false`, and each
    other kind's registration requires the push:
    `ProviderConformanceTests.AUTH_OIDC_006_AC4_TheDocumentNamesThePushAndLeavesItsRequirementToEachClientAsync`;
  - CORS on `POST /oidc/token`, discovery and `GET /oidc/jwks` for the origin of each
    registered public-application return address, that origin exactly, no credentials,
    the token endpoint's preflight answered with `POST` and `Content-Type`; any other
    origin and every other route gets none:
    `PublicApplicationTests.AUTH_OIDC_006_AC6_ARegisteredOriginIsAnsweredForItselfAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_AC6_AnyOtherOriginGetsNoCrossOriginHeaderAsync`,
    `PublicApplicationTests.AUTH_OIDC_006_AC6_NoOtherRouteAnswersCrossOriginAsync`;
  - the mail server accepts a public application's token for an audience it names, the
    client it accepts or the application itself:
    `MailServerTokensAgainstStalwartTests.INT_MAIL_004_AC5_TheServerAcceptsAPublicApplicationsTokenForAnAudienceItNamesAsync`
    (integration, against the release INT-MAIL-001 names).
- AUTH-OIDC-006 AC4's registration metadata: no endpoint publishes a client's
  registration metadata (AUTH-OIDC-001 AC3), so `require_pushed_authorization_requests:
  true` is held as each other kind's requirement in the registry, and decided by the
  refusal of its direct request above.

## 2. Items not implemented

None. The two questions below are answered in the code by refusal until the lead
answers them.

## 3. Resolved by rule

- `src/Janus.Hosting/Bff/ProviderProbes.cs`, `tests/Janus.Conformance.Tests/ConformanceSuiteTests.cs`:
  the conformance suite reported a discovery document whose
  `require_pushed_authorization_requests` was not `true` under
  `auth.oidc.nonconformant`. AUTH-OIDC-006 AC4 (D-252) now requires `false`, so the
  suite would report every conformant deployment. Rule: the check goes, since a direct
  request is no longer a form AUTH-OIDC-006 retires for every client and LIB-TEST-001
  AC4 reports only retired forms.

## 4. Open questions

1. Open, Tier 2. AUTH-OIDC-001 (D-166, 340) says registering a client the registry
   holds changes its kind and leaves its secret. A `public-application` client holds
   none (D-252), and the registry's checks tie the secret to the kind. Readings: (a) a
   change of kind across that line is refused, the client being removed and registered
   anew; (b) a change into `public-application` removes the secret and its replaced
   secret, and one out of it draws a secret as a first registration does. Smallest fix
   for (a): none, the code refuses it `api.request.malformed` naming `kind` and changes
   nothing (`ClientRegistryTests.AUTH_OIDC_001_AClientIsNotMovedAcrossTheSecretLineAsync`);
   for (b): the registry clears or draws the secret in the same unit of work, and the
   audit row says which.
2. Open, Tier 2. No chapter says what a push naming no `redirect_uri` does; the code
   has taken the client's one registered destination. A public application may hold
   several. The code refuses such a push from it `invalid_request` with no reference,
   as RFC 6749 section 3.1.2.3 requires a named destination where several are
   registered; a client holding one is answered as before. The other reading takes the
   first registered address. Smallest fix for the first: a sentence in AUTH-OIDC-006;
   for the second: `RegisteredDestination` takes `OidcClient.Redirect`.
3. Open, Tier 2. LIB-TEST-001 AC4 asks the suite for each retired form that needs no
   signed-in person. Until now the discovery document's requirement stood for the
   direct request; with it gone (section 3), no probe asks a direct request of the
   sign-on client, which is not a public application. Readings: (a) the suite stays on
   the back channel; (b) it gains a probe of `GET /oidc/authorize` with parameters from
   the sign-on client, expecting the 400 answer whose `details.error` is
   `invalid_request` (LIB-API-003 AC4). Smallest fix for (b): one probe and a reading
   of that answer's details in `ProviderProbes`.

## 5. Gate result

- Fast checks on every commit: the build with warnings as errors, the analysers and
  `dotnet format --verify-no-changes` are green.
- Unit and contract tests before the push: 5291, green.
- Commit-message gate, the other gates and the full-history secret scan before the
  push: green.
- The pipeline's first run, on 92f50779, failed one integration test:
  `ConformanceSuiteTests.AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync`
  still counted the discovery requirement among the findings (section 3). The count
  is corrected in ebe7f686.
- Gate of record: the pipeline's run on this report's commit, whose identifier and
  result the round's last message gives.
