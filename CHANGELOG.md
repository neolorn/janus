# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
against the public contract of LIB-API-001.

## [Unreleased]

### Added

- `Janus.Conformance`, the suite a host runs against its own deployment, each call
  answering a report whose findings are codes with structured data.
  `ConformanceSuite.Policies` names each entity of the host's context that is not a
  declared resource type, the rows of a declared relationship or a contract table, under
  `authz.policy.unregistered`. `ConformanceSuite.Declaration` judges a declaration by
  the checks startup runs and reports a refusal under its own code.
  `ConformanceSuite.TruthTableAsync` writes each case of the host's table into the
  deployment and reports, under `authz.truthtable.disagreement`, each case the single
  check or the list filter decides otherwise than the table states; it writes into the
  database, so it runs against a deployment kept for it.
  `ConformanceSuite.ProviderAsync` asks the provider each form AUTH-OIDC-006 retires and
  reports, under `auth.oidc.nonconformant`, each one it admits or its discovery document
  lists.
- `IResources` in `Janus.Core`: a host registers each record it creates, many at once
  for an import, and moves one, inside its own unit of work, and the ancestry the
  permission filter reads is written in the same transaction. A record is placed only in
  a container of the type its own is declared contained in and of the same organization;
  anything else is refused as `api.request.malformed` naming `resourceType`,
  `resourceId` or `containedIn`, and a refused batch writes nothing. A record of a
  sensitive type names a subject holding an account that is neither being deleted nor
  deleted, or it is refused naming `subject`.
- A person signs in, registers or links an identity with Google or Apple.
  `GET /auth/providers/{provider}` with `intent` of `signin`, `register` or `link` and a
  local `returnTo` sends the browser to the provider with a single-use state and nonce,
  and a proof key where the provider's document lists `S256`; the provider returns to
  `/callbacks/providers/{provider}/return` on the machine profile, by `GET` or by a form
  post, and the browser is sent on to `/auth/providers/{provider}/return` to finish on
  the browser profile. The state is judged once in fixed time against the round trip the
  same browser started, and the identity token against the provider's keys, issuer,
  client and nonce. A linked identity signs in with a delegated session, which never
  satisfies a step-up gate. Registering takes the address the provider vouches for
  without a code where the provider operates the mailbox, and sends one code otherwise;
  an address already registered answers exactly as a new one does. A refusal returns the
  browser to `returnTo` with the code in `error`. A host declares each provider with a
  `SocialProvider`: the address of the provider's discovery document and of its document
  naming its issuer and keys, the deployment's client identifiers there, the address the
  provider returns the browser to, and the client secret. Startup stops a deployment
  under `model.startup.declarationmissing` where a provider is declared twice, is not a
  social provider, has a keys address that is not HTTPS or has no client, and, naming
  `configuration`, `return` or `secret`, where its discovery address is not HTTPS, its
  return address is not HTTPS or does not end in
  `/callbacks/providers/{provider}/return` for the provider it is declared for, or its
  secret is empty.
- `POST /account/link/{provider}` answers `204` where the signed-in account may link the
  provider, and `DELETE /account/link/{provider}` unlinks it at the provider-unlink
  step-up. Unlinking the only way left to sign in to the account, here or through the
  credential endpoint, is refused with `409 identity.link.lastcredential`.
- A round trip to a social provider is kept in a table of the library's schema while the
  browser is away, one row per browser. The proof key is kept wrapped under the
  key-encryption key and is re-wrapped with the rest when the key is rotated; the row
  goes when it is taken or when the session it belongs to ends.
- The annual operation on the envelope, in which the key-encryption key is rotated, is
  warned of: a daily job, `envelope-rotation`, raises `expiry-approaching` from
  `maintenance.expiry.warninglead` before a year has passed since the last
  `envelope-rotation` entry of the maintenance log, and goes on raising it until the
  next one is recorded. A log that records none has the operation due at once.
- `janus register-client` registers a client in the provider's registry, or changes a
  registered one, from the server: `--client`, `--name`, `--kind`, `--redirect` and
  `--scopes`, with the secret piped in the key document as `clientSecret`. The registry
  keeps what the secret hashes to. Registering a client again with a new secret keeps
  the one it replaced accepted for the access-token lifetime and five minutes, which is
  how a client secret is rotated. Each registration is recorded as
  `auth.oidc.clientregistered`.
- The audit trail's monthly partitions are kept by a daily job, `audit-partitions`,
  under the maintenance credential: the current month and the two after it are created
  where missing, and partitions past `retention.audit.security` or
  `retention.audit.routine` are dropped. The job refuses any other credential, and each
  run is recorded as `ops.auditpartitions.maintained`.
- The restore test runs by itself every `backup.restoretest.interval`. A deployment
  registers `IRestoreTestInstance`, which restores its latest backup into a throwaway
  instance and tears it down again; the library opens the restored database with the
  keys it runs on, decrypts the canary's field, finds the canary's account by its
  verified email, and times the whole against `backup.restoretest.objective`. Every run
  is recorded as `ops.restoretest.completed` with its outcome, the seconds it took and
  the objective. A run that restores nothing, cannot decrypt, cannot find the account,
  runs past the objective (it is abandoned there) or whose instance may still stand
  raises `restore-test-failed`. Without an `IRestoreTestInstance`, every run raises it.
- `replay-erasures <ledger path>` carries out again, after a restore, every erasure the
  off-host ledger records and the restored database does not: from whatever state the
  restore left the account in, with the host told again, audited under the principal
  `replay-erasures`. The whole ledger is read first and a line in any other form refuses
  it whole; a second run changes nothing more. It prints how many lines were carried out
  again, stood already, or named no account.
- A deployment can register `IErasureLedger` over storage that does not share fate with
  the database host. Every erasure's line (its instant to the second, the subject
  identifier and the reason) is appended to it before the erasure completes: the line is
  a required confirmation named `erasure-ledger`, retried and raised as any required
  subscriber is and listed first at `GET /admin/erasures/{id}`, and the manual
  completion appends it itself and answers `system.fault` while the ledger refuses it. A
  deployment that registers no ledger completes its erasures without one.
- `no-emergency-credential` is raised by the hourly `emergency-credential` job for as
  long as no break-glass credential stands, including after one is spent, and stops only
  when one is generated.
- A deployment can register `IClockReference`, which reports how far the host's clock
  stands from the time the environment keeps it to, and `ICertificateRenewal`, which
  reports when the last certificate renewal failed. The hourly `clock-drift` job raises
  `clock-drift` when the offset exceeds `factor.totp.drift` steps of 30 seconds, and the
  hourly `certificate-renewal` job raises `certificate-renewal-failed` until a renewal
  succeeds. Either one missing, or unable to answer, is raised as `degradation`.
- A permission a host declares with the action `export` is an export operation. While
  `exfiltration.export.stepuprequired` is on, exercising one asks the session for
  step-up even where the host bound it to no gate, so a system principal cannot export
  until the deployment turns the flag off. Each person or principal is admitted
  `exfiltration.export.ratelimit` exports in any rolling hour, and the next is refused
  with `auth.throttled` and `retryAt`. Every admitted export is recorded as
  `authz.access.exported`, naming who exported, the operation, the kind of record and
  the one record a check named. A check, a list filter and an SQL fragment exercising
  the permission each count as one export; a capability page does not.
- A host reports through `IReadVolume` how many records each gate-filtered query or
  export returned to a person. Each person's count for the day in
  `privacy.calendar.timezone` is compared with their own daily mean over
  `exfiltration.readvolume.baselinewindow`, recomputed by the daily
  `read-volume-baseline` job, and `read-volume-anomaly` is raised when it exceeds both
  `exfiltration.readvolume.factor` times that mean and
  `exfiltration.readvolume.minimum`. Work done by a system principal is not counted.
- Two sessions of one account used inside `alerting.sessions.window` from cities further
  apart than `alerting.sessions.distance`, or in different countries, raise
  `concurrent-sessions-implausible` for the account, naming the two sessions and neither
  place. Ordinary use on several devices in one city or nearby raises nothing.
- The city shown on a session is resolved by the library from the address the session
  was used from, and is a field of no request. A deployment can supply its IP-to-city
  file through `ILocationSource`, in the format the interface documents. Sessions then
  show the city and country each was used from, resolved in process against a copy read
  on first use and refreshed by the `location-database` job every
  `location.database.refresh`. A file that cannot be read whole is refused and the copy
  held before it kept. While the file is missing, refused or stale, a session is shown
  without a location and `degradation` is raised once a window.
- The daily `holiday-list` job raises `holiday-list-exhausted` when no date in
  `privacy.holidays` falls beyond `maintenance.expiry.warninglead`, an empty list
  included. Deadlines are counted on the dates the list holds; the alert only asks for
  the next ones.
- Licence and permit expiry dates are kept and read at `/admin/compliance/licences`, and
  the daily `licence-expiry` job raises `expiry-approaching` for each one within
  `maintenance.expiry.warninglead`, a lapsed one included. The maintenance log is read
  and appended at `/admin/compliance/maintenance`, each entry carrying the person who
  recorded it; no route changes or removes an entry and the database role cannot. Both
  answer to `compliance:manage`.
- A message no transport took is carried again. The `sends` job retries it under
  `outbox.retry.*` in the languages still owed, judged by the restrictions and held by
  the gateway floor as any send is, and counts it only once a transport takes it. Once
  the budget is spent the message is removed and `degradation` is raised for its
  channel, naming the message and never where it was going.
- More permission refusals than `alerting.denials.threshold` against one actor inside
  one fixed ten-minute window raise `denial-spike` for that actor. The refusals of
  requests that name no acting subject are counted together and raised with no scope.
- The library carries the events it emits, each deriving from `DomainEvent`. An
  operation writes each event onto its own transaction, and the `events` job offers it,
  once that transaction has committed, to every `IEventConsumer<TEvent>` the host
  registered for its kind. A consumer that refuses or throws is offered the event again
  under `outbox.retry.*`, the others are not, and once the budget is spent the event is
  failed and `degradation` is raised. An event every consumer has taken is removed by
  the expiry sweep; a failed one stays. A host need not register an `IEvents`; one that
  does keeps it, and the library's delivery is bypassed.
- `configure` changes protected keys from the server, the one way to change a key the
  management application refuses: pipe the key document to it as to `bootstrap` and name
  each key as `--<key> <value>`, with `--reason`. It takes the keys chapter 10 section
  4.8 protects and `stepup.enforcement.<organization>` for an organization the
  deployment holds, and refuses every other key. Each change is recorded under the
  `configure` principal with its reason and raises `protected-setting-changed`; the
  governing language also raises `governing-language-changed`. A change that would leave
  the deployment unable to start is refused and nothing of it is written.
- A change to the system policy or to an organization's policy that leaves any step-up
  gate asking less (a lower level, phishing resistance dropped, or a longer maximum age)
  raises the High `stepup-policy-weakened` alert as it is made, naming the policy key
  and the gates. An organization's alert is raised under that organization.
- `rotate-fingerprint-key` rotates the fingerprint key from the command line under the
  maintenance credential, as `rotate-kek` rotates the other: add the new version as
  current, keep the previous one, restart the application on it, and pipe the document
  to the command. It computes every stored fingerprint again from the value beside it,
  resumes where it stopped, and prints the new version's escrow copy. Once the copy is
  sealed, `rotate-fingerprint-key --sealed` retires the previous versions; it refuses
  while a username held after an erasure, or an address an erased account gave up, is
  still reserved under one of them. Retirement forgets the throttle and sending counts
  kept under a previous version, so any of those not touched since the new version
  became current start again from nothing. A social sign-in link also holds the
  provider's subject encrypted under the account's key.
- `rotate-kek` rotates the key-encryption key from the command line under the
  maintenance credential. Add the new version to the secrets manager as current, keep
  the previous one, restart the application on it, and pipe the document to the command:
  it re-wraps every value held under the key in batches of 500, resumes where it stopped
  when run again, and prints the new version's escrow copy. Once the copy is sealed,
  `rotate-kek --sealed` retires the previous versions and names them for removal from
  the secrets manager; it refuses while anything is still wrapped under them. Each step
  is audited under the `rotate-kek` principal. Refresh tokens issued before the rotation
  stop reading once the previous version is removed.
- A command-line application stands a fresh deployment up with `bootstrap`. It takes the
  organization's name, the first administrator's email and phone, optionally the
  corporate address whose mailbox is queued for them, and each required deployment value
  as `--<key> <value>`; any other key is refused. The database connection, the
  key-encryption keys and the fingerprint keys are read once from a JSON document piped
  to standard input, never from a terminal, an argument or the environment. It writes
  the named values, the three administrative roles, the administrative organization and
  its policy, the administrator, the reserved `emergency` account holding the role and
  no way in, and the restore test's canary, raises the alert that no emergency
  credential exists, and prints the administrator's `/enrol` address. It refuses to run
  where a system administrator exists or ever existed. A refusal is one JSON line on
  standard error, with exit code 1. What it defines and sets is audited under its own
  principal, for which `SystemOperation` carries `Bootstrap`.
- The library runs its own scheduled work. A worker `AddJanus` registers sweeps expired
  sessions, codes, links and tokens, ends the windows of account deletion, organization
  erasure, loss reports and privacy-request deadlines, re-verifies locked domains,
  publishes the outbox, provisions mailboxes, carries raised alerts, reconciles the mail
  server daily and reads the gateway balance. Each job runs as a named principal of its
  own, once across the processes of a deployment, and a job whose last success is older
  than twice its interval raises `background-job-failed`. `SystemOperation` carries
  `Delivery` and `Monitoring` for this work, and the runs are kept in a table of their
  own.
- Background work acts as a named system principal that states its reason, and is
  audited as one. The passes that record what they do (the account and organization
  erasure sweeps, the privacy-request deadline sweep and the loss-report windows) run
  only as a principal that may sweep what has expired, and are refused to a person or to
  a principal named for other work. What they record carries the principal's name and
  reason where a person's action carries the acting account.
- The sealed break-glass credential. `POST /admin/break-glass/generate` generates it,
  for a stepped-up system administrator or from a break-glass session, and answers the
  code once, in nine check-charactered groups of four, with the absolute `/break-glass`
  address the envelope prints; a new code invalidates the one before it, and only an
  Argon2id hash of it is kept. `POST /auth/break-glass` takes the code on the machine
  profile, ignoring any cookie the browser holds, and opens an auth session for the
  reserved `emergency` account that passes every step-up gate for
  `breakglass.session.lifetime`. A code opens one session; a group whose check character
  is wrong is refused before any hash is compared; at most five attempts an hour are
  taken from all sources together, besides the per-source delay. Generation and use are
  audited under `auth.breakglass.generated` and `auth.breakglass.used`, and raise
  `breakglass-used` to the operator and to the owner whatever `alerting.owner.enabled`
  says. The reserved account is never suspended, taken down, deleted, granted anything
  or added to a group, and is given no password, identifier, factor, recovery codes or
  mail credential; each is refused with `authz.denied`. The reserved account is marked
  on its row, and the credential and its attempts are kept in two tables of their own.
- `POST /callbacks/providers/google` and `POST /callbacks/providers/apple` take the
  security events Google (Cross-Account Protection) and Sign in with Apple send about an
  identity linked to an account, on the machine profile and held to
  `integration.callback.ratelimit`, from each provider the host declares with a
  `SocialProvider`. An event is verified against the keys the provider publishes,
  carried once by its `jti`, and audited under `auth.providerevent.taken` or
  `auth.providerevent.rejected`. A compromised, disabled or signed-out provider account
  ends every session of the account and holds the linked credential until the person
  signs in by another factor, which restores it under `auth.credential.restored`;
  withdrawn consent or a deleted provider account unlinks the credential, or suspends
  the account with a security notice where it is the last way in; a disabled relay
  address drops to unverified. An event of an undeclared provider, or one the keys do
  not verify, is refused as every rejected callback is. The client the documents are
  read with is `identity-providers`.
- `UseCallback` mounts one of the host's own providers' callbacks on the machine
  profile, at a path the host chooses and ahead of the browser profile. A signed
  callback (`ISignedCallback`) names its provider's keyed hash, where the signature and
  the signed bytes are, its secrets and its event identifier; the library verifies the
  signature over the raw bytes in fixed time against the current secret and, for 24
  hours after a rotation, the previous one, holds a five-minute window where the scheme
  carries an instant, and carries each event once, giving the claim back when the host's
  route does not answer with a 2xx. An unsigned callback (`IUnsignedCallback`) reaches
  the route only with a reference issued for it and once the host has confirmed it with
  the provider. Both are held to `integration.callback.ratelimit` and to the provider's
  published ranges first, and every refusal is answered 429
  `integration.callback.rejected`, recorded against its source and counted toward
  `alerting.callback.threshold`. Claimed events and issued references are kept, as
  hashes, in the `callback_events` and `callback_references` tables.
- `ICallbackReferences.IssueAsync` issues the correlation reference an unsigned callback
  carries: 128 random bits in base64url, of which only the hash is kept.
- `GET /callbacks/sms/dlr` takes the SMS gateway's delivery report on the machine
  profile. `ISmsTransport.ReadReport` reads the report from the parameters the gateway
  puts in the query string, and every transport implements it. A report of failed
  delivery for a send the library made releases that send from its restrictions and
  nothing else; a report carrying an unknown reference, or one the transport cannot
  read, is refused 429 `integration.callback.rejected`.
- `SensitiveBodyAttribute` marks an endpoint whose request and response bodies never
  reach the framework's request logging, whatever fields the deployment or the endpoint
  asks it to record. Every endpoint the library maps carries it, and a request the
  logging meets before its endpoint is known is treated as marked.
- An administrator holding `membership:manage` can invite a person into an organization:
  `POST /admin/organizations/{id}/invitations` binds an `email`, a `phone`, both or
  neither, and may attach `roles` (which also asks `grant:manage`) and `documents`
  (fixed at their current version). It asks step-up and answers 201 with the
  invitation's `id` and `expiresAt`; the link is sent to the bound email, and where no
  email is bound the `token` is answered once for the administrator to hand over. An
  invitation into the administrative organization of a deployment that registers
  `IMailServer` needs both a personal `email` and a `corporateEmail`, and reserves that
  mailbox disabled; inviting the address again replaces an expired invitation.
  `DELETE .../invitations/{invitationId}` revokes an invitation nobody has acknowledged
  (204, or 422 `identity.invitation.expired` once it is), and gives up a mailbox nobody
  ever held. What an invitation binds is held encrypted and forgotten when it is
  revoked; its link is held only as a fingerprint. Invitations are kept in a table of
  their own, and a deployment that registers its own message templates carries
  `invitation-link`. `IInvitations` is the same set of operations in process.
- Every hash, token, code and fingerprint the library compares in process is compared
  in constant time, the fingerprints of a registration link included.
- A registration can be begun from an invitation link: `POST /register` takes an
  `invitationToken`, which spends the link. The email the invitation bound is verified
  by that press and locked, a bound phone is locked, taken at its step only as bound,
  and cannot be skipped, and the inviting organization's login factors and domain lock
  govern the steps. The account the terms step creates holds the invitation and no
  membership. A token that opens nothing answers 422 `identity.invitation.expired`, and
  a bound email an account already holds answers 422
  `identity.invitation.identifiermismatch`, so its holder signs in instead.
  `IRegistration.BeginAsync` takes the token.
- A person already signed in who presses an invitation link has the invitation attached
  to their account: `POST /register` with `invitationToken` from a signed-in browser
  attaches it and answers 409 `identity.registration.signedin`, as any registration from
  a signed-in browser does, and a token that opens nothing answers 422
  `identity.invitation.expired`. `IInvitations.OpenAsync` is the same operation in
  process.
- `GET /account/invitation` reads the invitation attached to the signed-in account for
  the membership step: its `id`, the `organization` and its `organizationName`, the
  display name of who invited them as `invitedBy` (null where their account shows none),
  the `roles`, the `documents` with their `version`s, and `expiresAt`. Where the account
  opened several links, the last one still standing is read. An account with none
  attached answers 404 `identity.invitation.notfound`. `IInvitations.AttachedAsync` is
  the same operation in process.
- An account can hold a personal email that a membership keeps: while it is kept it
  stays verified and non-primary, every security notice reaches it whatever the backup
  setting, and removing, promoting or replacing it answers 409
  `identity.identifier.locked`; the account view shows it `locked`. The identifiers
  table marks such an email with `is_personal`.
- `POST /account/invitation/acknowledge` with `{ "invitationId" }` acknowledges the
  invitation the membership step showed (204): the membership attaches carrying the
  documents at their versions and when, each role is granted across the organization as
  given by who invited them, and the audit trail records
  `identity.invitation.acknowledged`. Where the organization's mail is integrated, the
  corporate address becomes the primary email, verified and locked, the personal email
  stays beside it, the mailbox is owed enabled, and the account's notice set is told of
  the address. Nothing attaches until the account meets the organization's required
  assurance and credential redundancy with the factors that organization permits (403
  `auth.stepup.required`, outcome `enrol`, naming the `field` and its `value`); a bound
  identifier not verified on the account answers 422
  `identity.invitation.identifiermismatch`, an invitation that is revoked, acknowledged
  or expired 422 `identity.invitation.expired`, and one the account does not hold 404
  `identity.invitation.notfound`. The export carries `acknowledgedAt` on a membership
  and a `membership-acknowledgements` section. The memberships table holds the
  acknowledgement. `IInvitations.AcknowledgeAsync` is the same operation in process.
- An invitation that expires unused forgets what it bound when the invitation sweep
  runs; the row keeps who invited into what and when, and its mailbox stays reserved.
  Erasing a subject forgets what an invitation attached to their account binds in the
  same transaction as the erasure.
- `DELETE /admin/organizations/{id}/memberships/{subject}` ends a membership under
  `membership:manage` (204); the account, its state, its grants and the organization
  persist, and the audit trail records `identity.membership.ended`. An account holding
  no current membership of the organization answers 400 `api.request.malformed` naming
  `subject`. Ending a membership of the administrative organization retires the
  corporate address in the same transaction: the address leaves the account and is free
  for a later invitation, the personal email becomes the primary, the mailbox is owed
  disabled, and the notice set is told of the new primary.
  `IInvitations.EndMembershipAsync` is the same operation in process.
- `POST /admin/accounts/{subject}/suspend` and `/reactivate` suspend and reactivate an
  account under `account:manage`, each a step-up action (204). Suspension ends every
  session of the account in the same transaction; reactivation restores the account as
  it stood, so one suspended while restricted is restricted again. An account its owner
  deactivated becomes the administrator's to reactivate and its owner's link does not
  stand it up. Reactivation applies only to an administrator's suspension, and an
  account being deleted is not suspended (403 `authz.denied`); an unknown subject
  answers 400 `api.request.malformed` naming `subject`. The audit trail records
  `identity.account.suspended` and `identity.account.reactivated` in the security
  category. `IAccounts` is the same pair of operations in process. The accounts table
  carries `restriction_held` for the restriction held while the account is suspended or
  deleting.
- A processing restriction is kept while the account passes through a deletion window, a
  takedown or a suspension: cancelling the deletion, reversing the takedown and
  reactivating the account each bring it back restricted, and a restriction decided
  while the account is suspended or deleting is held for when it returns, with
  `RestrictionChanged` delivered to the subscribers when it is decided.
- `POST /admin/accounts/{subject}/restriction/lift` lifts a processing restriction under
  `account:manage` (204): the account is active again, `RestrictionChanged` is delivered
  to every subject-event handler in the same transaction, and the audit trail records
  `privacy.restriction.lifted`. An account that is not restricted, including one holding
  a restriction while suspended or deleting, answers 403 `authz.denied`.
  `IAccounts.LiftRestrictionAsync` is the same operation in process.
- `POST /admin/accounts/{subject}/delete/cancel` cancels a deletion inside its grace
  window on the subject's behalf under `account:manage` (204), whether the subject or an
  out-of-band erasure request began it; the account comes back as it stood and the audit
  trail records `identity.deletion.cancelled` naming the erasure request where one began
  the window. A takedown answers 409 `identity.takedown.active`, a closed window 422
  `identity.deletion.windowelapsed`, and an account in no window 403 `authz.denied`.
  `IAccounts.CancelDeletionAsync` is the same operation in process.
- `GET /admin/accounts/{subject}/photo` serves the photo an account shows to an
  administrator holding `account:manage`, as `image/jpeg` with
  `Cache-Control: no-store`. An account that shows none and one whose organizations
  withhold photos both answer 404; an unknown subject answers 400
  `api.request.malformed` naming `subject`. `IAccounts.ReadPhotoAsync` is the same read
  in process.
- `GET`, `POST /account/mail/apppasswords` and `DELETE /account/mail/apppasswords/{id}`
  list, create and revoke the signed-in person's mail app passwords at the mail server.
  The library issues the person a token to the mail server's client from their session,
  naming that client in `aud`, and makes one call with it; the server generates the
  secret, which is answered once, stored nowhere and never logged: `IssuedAppPassword`
  and its `Secret` carry `NeverLogged`. Creation and revocation are the
  `mailcredential:create` and `mailcredential:revoke` step-up actions, notified to the
  security-notice set and audited as `auth.mailcredential.created` and
  `auth.mailcredential.revoked` by the server's identifier. An account that holds no
  enabled mailbox is refused with 403 `authz.denied`. `IMailServer` carries
  `AppPasswordsAsync`, `CreateAppPasswordAsync` and `RevokeAppPasswordAsync`, and a
  deployment that registers a mail server must declare `MailServerClient` or it does not
  start. `IAppPasswords` is the same operations in process.
- `GET /admin/erasures` lists every erasure whose host-side work is outstanding, oldest
  first, and `GET /admin/erasures/{id}` reads one, each with its subject, reason,
  status, attempts and every registered subscriber with when it confirmed.
  `POST /admin/erasures/{id}/complete` closes an erasure whose retries were spent, asks
  the `erasure:complete` step-up, and is audited as `privacy.erasure.completed` with the
  required subscribers that had not confirmed; one not yet failed is 409
  `privacy.erasure.notfailed`. An erasure's `id` is the identifier of its delivery; an
  identifier naming no erasure is 404 `privacy.erasure.notfound`. All three need
  `privacyrequest:manage`. `IErasures` is the same operations in process.
- `GET /admin/access?resourceType=...&resourceId=...` answers who can access a record:
  every live grant on it, on what contains it and on the whole organization, nearest
  first, each with its kind, holder, role, whether it denies and the container it sits
  on. It needs `grant:read` in the record's organization; `resourceType` `organization`
  asks for the whole of one. `IAccessGate.WhoCanAccessAsync` is the same in process, and
  given the host's `FilterSources` it also reports each holder a derivation confers the
  record on as a derived grant; where `authz.reverselookup.budget` runs out first the
  answer carries `partial: true` and the relationships left unevaluated. Without those
  rows, a record a derivation reaches is refused with `authz.derivation.sourcesmissing`,
  over HTTP included.
- Staff mailboxes are provisioned through `IMailServer`, which a deployment registers
  where its staff mail is hosted and which no package ships. A mailbox is owed
  `disabled` from its reservation, `enabled` while its holder is an active member of the
  administrative organization, and `disabled` otherwise; `MailboxPublisher` pushes
  whatever differs under a key that stays the same until the server confirms it, retries
  on the outbox schedule, and raises `degradation` naming the mailbox when the budget is
  spent. `MailboxReconciliation` compares the server's listing with what is owed and
  raises `degradation` on any difference without changing either side. The address is
  held encrypted under its holder's key and is erased with them.
- Where Continue with Apple is among the system policy's `loginFactors` and
  `notification.email.sendingdomain` is not in `notification.email.relayregistered`, the
  deployment raises `relay-domain-unregistered` with the domain as it starts and
  whenever a change to either key or to `policy.default` leaves it so. The warning stops
  neither the start nor the change; a warning that cannot be raised stops both.
- An organization can lock its members to email domains it has proved by DNS:
  `POST /admin/organizations/{id}/domains` lists a domain and answers the TXT record to
  publish, `POST .../domains/{domain}/verify` looks for it (422
  `identity.domain.unverified` where it is not found), `DELETE .../domains/{domain}`
  removes the domain and raises `domain-removed`, and `GET` lists each domain with its
  last check. From the first domain listed, a member who signs in with an address
  outside the verified list, or with an address in a removed domain, is refused with 422
  `identity.identifier.domainnotallowed` once a factor has succeeded, and a sign-in link
  or code is not sent to such an address. Each change asks `domain:manage` in the
  administrative organization, step-up and a reason; listing and verifying also ask
  `system:administer`. `DomainReverification` re-checks every verified domain each
  `domain.reverify.interval`, and a failed check raises `domain-reverification-failed`
  and revokes nothing. The deployment registers an `IDnsResolver`; without one no domain
  is ever verified. `policy.default` refuses a non-empty `emailDomains`. Domains are
  kept in a table of their own, and a sign-in and a sign-in link record the address they
  were opened with. `IOrganizationDomains` is the same set of operations in process.
- `GET /admin/organizations/{id}/policy` answers the policy an organization's members
  resolve to, each field and each gate as `{ value, overridden }`, and
  `PUT /admin/organizations/{id}/policy` replaces what the organization overrides. Both
  ask `organization:manage` in the administrative organization; a replacement also asks
  step-up under `policy:change` and a reason, and `system:administer` where it loosens.
  A field looser than the system policy is refused with 422 `config.policy.belowsystem`
  naming it, the administrative organization cannot fall below `aal2` (422
  `config.value.belowfloor`), and `emailDomains` or an unknown member is refused
  with 400. `IOrganizations.PolicyAsync` and `ReplacePolicyAsync` are the same in
  process.
- `POST /admin/organizations` creates an organization with its policy key holding no
  override; `POST /admin/organizations/{id}/delete` suspends one, ending every session
  of its members, and `POST /admin/organizations/{id}/delete/cancel` restores it inside
  `organization.deletion.grace` (422 `identity.deletion.windowelapsed` after). All ask
  `organization:manage` in the administrative organization and a reason, and are
  recorded in the audit trail; the deletion and its cancellation also need step-up under
  the gate `organization:delete`, and the administrative organization is refused with
  409 `identity.organization.protected`. `IOrganizations` is the same set of operations
  in process. While an organization is suspended nothing in it is reached through any
  grant, a derivation over the host's own rows included: a grant of an organization
  whose deletion has been requested confers nothing from the next request, in checks,
  filters and capability arrays alike, `identity.effective_grants` leaves such grants
  out, and they confer again once the request is cancelled. The host's facts are left as
  they stand for a cancellation to restore.
- `GET /admin/groups?organization={id}` reads an organization's groups with their direct
  members, `POST /admin/groups` creates one, `DELETE /admin/groups/{id}` removes one
  that holds no member, belongs to no group and was never given a grant (409
  `authz.group.inuse` otherwise), and `POST|DELETE /admin/groups/{id}/members` adds or
  takes out an account or a group of the same organization (409 `authz.group.cycle`
  where the group would contain itself). All ask `group:manage` in the group's
  organization and a reason, and are recorded in the audit trail; a change of members
  also needs step-up, and `system:administer` where the group reaches a role carrying
  it. `IGroups` is the same set of operations in process. A group the deployment holds
  no row for belongs to no organization, so every caller is refused it with 403
  `authz.denied`, as a caller without `group:manage` is.
- `GET /admin/roles` reads every role with its permissions, `POST /admin/roles` creates
  a role or gives an existing one the permissions stated, and
  `DELETE /admin/roles/{name}` removes one no grant or derivation names (409
  `authz.role.inuse` otherwise). All ask `role:manage` in the administrative
  organization; changes need step-up and a reason, are recorded in the audit trail with
  the permissions before and after, and need `system:administer` where the role carries
  it before or after. `IRoles` is the same set of operations in process.
- `POST /admin/grants` writes a stored grant to an account or a group on one registered
  record or, as `resourceType` `organization`, on a whole organization, and
  `DELETE /admin/grants/{id}` revokes one; both ask `grant:manage` in the grant's
  organization, step-up and a reason, and record who acted and when. A grant or
  revocation of a role carrying `system:administer` also needs `system:administer`.
  `IGrants` is the same pair of operations in process. A revocation naming no grant, and
  a grant on a record the deployment holds no registration for, are refused with 403
  `authz.denied` in the same way; a revoked or derived grant answers 404
  `authz.grant.notfound` only to a caller holding `grant:manage` where it is scoped.
- `GET /admin/grants?organization=...&subjectType=user|group&subjectId=...` and
  `IGrants.HeldAsync` read the live grants one account or group holds in its own name in
  an organization, oldest first, each with its identifier, kind, role, what it is on,
  whether it denies, its expiry, and who granted it, when and why. It needs `grant:read`
  in that organization; a grant reaching an account through a group is read under the
  group.
- `GET /admin/restrictions` and `GET /admin/restrictions/{name}` read the named
  restriction set under `restriction:edit`, the shipped defaults included;
  `PUT /admin/restrictions/{name}` creates or replaces one and `DELETE` removes one,
  each behind step-up, with a reason and an alert for a loosening, which also needs
  `system:administer` in the administrative organization, and a restriction keyed to a
  host supplier the deployment did not register is refused there with
  `config.value.notallowed` naming the `supplier`; and
  `POST /admin/restrictions/{name}/grant` adds credit to one key under
  `restriction:grant`, behind step-up and with a reason. `IRestrictionSet` is the same
  set of operations in process.
- `GET /admin/config/{key}` reads one runtime key under `config:read`: its value in
  force and its default in the key's own JSON type, whether it is protected, and which
  way it loosens. `PUT /admin/config/{key}` changes it under `config:manage`; a
  loosening also needs `system:administer` in the administrative organization, step-up
  and a reason, a tightening asks nothing more, a protected key is refused with 422
  `config.key.protected`, and the alert destination keys tell the destinations they
  replace. `IConfigurationAdministration` is the same operation in process.
- `GET /admin/audit?subject=...` and `IAuditTrail` read every audit record naming one
  subject, what it did to others as well as what was done to it, most recent first,
  under `audit:read`: each entry carries its codes, identities, organization and plain
  details, never a value held under a subject's key, so it reads the same before and
  after erasure.
- `GET /admin/explanations/{correlationId}` resolves a refusal's correlation identifier
  to the permission and the principal for a caller holding `audit:read` in the
  administrative organization, whichever organization the refusal was recorded in
  (`IAccessGate.ResolveAsync`, which takes no organization), and
  `GET /account/explanations/{correlationId}` resolves one for the principal it refused
  where the refused type is not concealed (`IAccessGate.ResolveOwnAsync`).
- `POST /admin/accounts/{subject}/sessions/revoke` ends every session of one account
  under `session:revoke-account`, and `POST /admin/sessions/revoke-all` ends every
  session in the deployment under `session:revoke`, the caller's own included, each
  permission held in the administrative organization. Both answer 204.
  `ISessions.RevokeAccountAsync` and `ISessions.RevokeEveryAsync` are the same in
  process and take no organization.
- The minor takedown, as `ITakedowns` and `POST /admin/accounts/{subject}/takedown`:
  under `takedown:execute` and step-up, one transaction suspends the account into its
  `takedown.grace` window, ends every session of it, records the trigger and the reason,
  and writes the `TakedownExecuted` delivery on which the host stops its own processing
  for the subject. The answer carries `takedownId` and `erasureDue`. `AccountSuspended`
  follows the commit; no deletion notice and no `AccountDeletionRequested` do.
- `GET /admin/accounts/{subject}/takedown` reads the latest takedown of an account: when
  it was triggered, when its erasure runs, and which registered subscriber has confirmed
  it and when. An account never taken down answers 404 `identity.takedown.notfound`.
- `POST /admin/accounts/{subject}/takedown/reverse` restores a taken down account to
  active inside its window, with a reason, and publishes `TakedownReversed`. After the
  window it answers 422 `identity.takedown.windowelapsed`.
- `MembershipChanged` announces a membership beginning or ending, naming the membership,
  its organization and whose it is. The erasure at the end of an organization's deletion
  window raises one for every membership it ends, alongside `OrganizationErased`.
- The library ships two default declarations: `LawfulBases.Default`, the six lawful
  bases with the four properties the library branches on, and
  `SensitiveCategories.Default`, the eight sensitive-data categories. A deployment
  declares them instead of writing them out, and a deployment in another jurisdiction
  declares its own list with its own flags and the library changes not at all. Nothing
  in the library chooses a path by a basis or a category; the one category read by name
  is the children's, which is the children's column of the records of processing.
- The erasure at the end of an organization deletion window: when the window elapses,
  every current membership of the organization ends, what the organization was called
  becomes its own identifier, and the instant the erasure executed is written onto the
  row. No row is removed and the identifier goes on resolving. The `OrganizationErased`
  event carries the organization and how many memberships ended, and the erasure is
  written to the audit trail as `identity.organization.erased`. A window cancelled
  inside itself is never reached, and an erasure asked for before the window elapses
  writes nothing.
- Startup verifies that the database carries the schema this build was compiled against,
  before any other check reads a table and before the host's web server starts. A
  database behind the model answers `model.startup.schemamismatch`, names every
  migration still owed, and stops the application with a non-zero exit. The check
  applies nothing, so an un-migrated database is left exactly as it was found, and a
  database ahead of the model starts, which is the expand half of a rollout.
- The emitted contract carries four credential events: `CredentialEnrolled` when an
  authenticator reaches active, and `CredentialSuspended`, `CredentialRestored` and
  `CredentialInvalidated` as a loss report opens, is cancelled and completes. Each
  carries the credential, its catalogue entry and whose account it is, and the
  suspension carries when its window ends. None carries secret material.
- The client registry is the one list of return destinations. Every registered client's
  return address is read at startup, and a deployment holding one that is not an
  absolute origin does not start. Registration resolves the client identifier it is
  given against the registry as it takes it, and what a completed registration reports
  as the return is the address that client registered; no step after the first takes a
  destination at all. An identifier the registry does not hold registers the person
  exactly as a registered one does and stores the deployment's default client, named in
  the protected key `redirect.defaultclient` and read against the registry at startup,
  so the completion returns the person to it. A deployment that names no default starts,
  and such a completion returns nothing.
- An account shows a photo. `GET`, `PUT` and `DELETE /account/photo` read it, replace it
  and give it up, and the image is served through the session gate as `image/jpeg` from
  no address a cache could share. Availability is the organization's, held in the key
  `photo.enabled.<organization>` and off until an organization is given it; an account
  of no organization, and one whose organization shows none, is answered as an account
  with no photo. The library reads no image itself: a deployment declares an
  `ImageCodec`, which decides by content what an upload is, holds it to
  `photo.maxdimension` and answers the JPEG that is stored. The photo is held in a table
  and a port of its own, encrypted under the subject's own key like any other personal
  field: nothing that reads an account reads image bytes, a dump yields no photograph,
  and erasure of the key leaves the image unrecoverable. A deployment whose policy shows
  photos and which declared no codec does not start.
- Every runtime configuration change goes through one operation that classifies it,
  gates it and writes it down. A configuration key loosens the way its row states; where
  a row states nothing, a key with only a ceiling loosens upward, a key with only a
  floor loosens downward, and a flag loosens away from its default. A change that
  loosens the deployment, and any change to a key that has no direction, needs the
  step-up gate met and a written reason, and a loosening, the named restriction set and
  the alert destinations included, is refused unless the caller also holds
  `system:administer` in the administrative organization; a tightening asks nothing
  more. Both are recorded with who made it, the key, the value before and after, the
  direction, the reason and the time, and the record reads back by setting and by actor.
  Changing the alert destinations goes through the same operation, and a change with no
  reason is refused before the destinations being replaced are told.
- The library ships the words of every message it sends, in English and in Arabic. A
  deployment that registers a catalogue of its own keeps it; one that registers none
  sends out of the shipped texts. Startup refuses a deployment whose catalogue has no
  text for a declared language, or a text message that does not fit one message. A
  text-message template is checked against its budget with every place the library fills
  at its widest, so a template that fits as it is written but not once a code, a link or
  an alert's detail is in it stops the deployment instead of costing two messages at
  every send. Nothing is measured at the moment of a send. The notice sent when someone
  tries to register an address already held, or to change another account to it, points
  its holder to sign-in and to recovery.
- Publishing an event answers for itself. An operation records its event inside the
  transaction that made it true and commits nothing it could not publish, so a change
  never reaches the database without its event reaching a consumer. Every method of the
  public contract returns an outcome, `IEvents.PublishAsync` included.
- Every message the library sends is written to its own outbox table inside the
  transaction that made it necessary, carried from that row, and removed once a
  transport has taken it. An operation that fails sends nothing, a message undertaken by
  one that succeeds is not lost with the process, and a transport that refuses leaves
  the message waiting rather than dropped. The row holds the whole of the message
  encrypted under a key of its own.
- Startup refuses a declaration whose encrypted field names no subject column, a column
  the declared type does not hold, or one holding something that is not a subject.
  Ciphertext an erasure could not reach stops the deployment instead of reaching
  production.
- A host declares a retention floor for each data category its purposes are over, with
  `RetentionFloor` on the declaration builder, and `retention.<category>` defaults to
  it, so a deployment starts without a stored period for each category. Building the
  model refuses a category with no floor under `model.startup.declarationmissing` naming
  `retention.<category>`, and startup refuses a stored period below the floor with
  `config.value.belowfloor` naming the key and the floor. Startup also refuses a
  deployment that is open to minors (`registration.adultaffirmation` off) without a
  written-consent lawful basis to hold a child's data under.
- The audit trail answers "who was affected" after an erasure. Reading one subject's
  records goes through the index that carries the subject, and a record whose subject
  key has been destroyed comes back anonymised (what happened, when, to whom by opaque
  identifier) instead of failing the whole read.
- Records of processing are generated rather than kept.
  `GET /admin/ropa?format=template` answers with the regulator's template: a row a
  declared purpose carrying its data and subject categories, its lawful basis, the
  non-sensitive, sensitive and children's columns, the retention of each category
  longest first, the recipients, the disposal measures, the roles holding a permission
  that serves it, and the technical security measures, beside the hosting environment,
  location and cross-border basis the deployment configured. The children's column
  follows the `children` sensitivity category a resource type declares, like every other
  category, and a deployment that admits minors and declares no children's type carries
  the register flag `children-undeclared`, so an empty column is reported rather than
  read as no children's processing. A purpose added to the model is in the next register
  with no separate edit, and nothing of the inventory is stored. The three cells no
  query can answer (the data owner, the organisational security measures and the
  assessment links) are stated through `PUT /admin/compliance/assessments` and flagged
  until they are; so are a purpose missing an assessment its basis requires, a processor
  missing an agreement reference, and a data category whose retention period cannot be
  read. Recipients are declared on the model builder, where a host declares every
  processor of its own business. The shipped provider register,
  `ProviderRegister.Default`, is the four rows the library's own processing makes true:
  the mail server, the SMS gateway, the hosting provider and password screening. The
  records of processing apply the hosting provider and the SMS gateway to every
  deployment, since every deployment sends its text messages through a transport it
  registers, the mail server while the deployment uses the library's own mail transport,
  and password screening while screening is online. Each appears whether or not the
  deployment declared it, flagged for a missing agreement reference until it gives one,
  and a deployment that declared one of the four reports its own row in place of the
  shipped default.
- An account can take a copy of what is held about it. `GET /privacy/export` answers in
  two arrangements of one assembly: `format=human` is grouped and labelled for reading,
  `format=machine` is one flat object whose names are stable across exports, and both
  carry the same data. The export carries every group the account page shows the person:
  the account's whole standing, its profile, every identifier with its role and
  verification state, the backup settings, the preferences in force, the live sessions
  with the locations resolved at sign-in and at last use, the enrolled credentials and
  the password by property and label, how the recovery code set stands, the browsers the
  account is known at, its memberships, the roles it holds, the assurance it can reach,
  the terms version, notice version and affirmation the terms step recorded, and the
  consent and objection records. No secret material crosses. It is gated at the
  account's own reachable assurance, limited to `privacy.export.ratelimit` a rolling day
  (the refusal carries `Retry-After` and the instant the limit lifts), and it raises
  `ExportRequested` so that each host can produce its own half.
- A deletion grace window that runs out is carried through: the sweep erases every
  account whose window elapsed without a cancellation, an account in its own deletion
  window when `account.deletion.grace` elapses and a taken down account when
  `takedown.grace` does, in one transaction per account, and puts the erasure on the
  outbox in the same transaction. The subject identifier stays, the personal fields go
  with the subject's key, the audit trail and the deployment's own records are
  untouched, and the username is held for `retention.consent` before anyone can claim
  it.
- An account takes itself down and puts itself back up. `POST /account/deactivate`
  suspends it with `suspendedBy = self`, ends every session it holds, and sends the
  deactivation notice with the link `POST /account/reactivate` consumes; an account an
  administrator suspended answers `identity.account.adminsuspended` instead, and only an
  administrator stands it up. `POST /account/delete` starts the grace window
  (`account.deletion.grace`), answers with when the erasure runs, ends every session,
  and sends the deletion notice with the link `POST /account/delete/cancel` consumes
  anywhere inside the window; afterwards it answers `identity.deletion.windowelapsed`,
  and a deletion the deployment began as a takedown answers `identity.takedown.active`.
  Both requests require step-up.
- The outbox worker publishes erasure, restriction and export to every handler a host
  registers, keeps each confirmation on the delivery's own row, and closes the delivery
  only when every required handler has confirmed. A handler that refuses, or whose own
  store fails it, is offered the event again on an exponentially growing delay with full
  jitter (`outbox.retry.initial`, `outbox.retry.factor`) until
  `outbox.retry.maxattempts` is spent, at which point the delivery is marked failed and
  the exhaustion is alerted; an operator who has done the work by hand closes it, and
  the erasure's own row is closed with it. A handler already confirmed is never offered
  the event twice, and a retry carries the key the first attempt carried.
- A host declares which of its resource types each subject-event handler does the work
  for, and which purposes each consent and objection handler covers. A deployment that
  declares a resource type sensitive, or a purpose on an objectable basis, and registers
  nothing that names it does not start: the failure is
  `model.startup.declarationmissing` with `details.handler` naming what is missing.
  Startup refuses two subject-event subscribers registered under one name, and any
  subscriber named `erasure-ledger`, with `model.startup.subscribername` naming it,
  since a confirmation is recorded under the name and a shared one would let an erasure
  close with a subscriber's work undone.
- A data subject request enters a queue with a statutory clock on it. A subject submits
  a restriction or a rectification for themselves at `POST /privacy/requests` and is
  answered with the request identifier, the receipt timestamp and the date the decision
  is due by; an authorised human enters a request that arrived out of band at
  `POST /admin/privacy/requests`, recording how it arrived, what confirmed the requester
  is the subject, and the date it reached the company. The deadline is six working days
  counted on the deployment's own week (`privacy.workingdays`), its holidays as
  currently listed (`privacy.holidays`) and its zone (`privacy.calendar.timezone`),
  never on a Monday to Friday assumption. Undecided requests raise a Normal alert
  `privacy.request.warninglead` before the deadline and a High alert on the deadline
  day, without anyone watching; a restriction still undecided when the deadline passes
  is granted and the account is restricted, and a request the system cannot grant by
  itself is recorded as deemed refused by lapse, with the subject told honestly and the
  record kept. Fulfilling a restriction restricts the account and tells the registered
  subscribers; fulfilling an out-of-band erasure starts the deletion grace window.
  Deciding a request tells its three refusals apart for the member of staff working the
  queue: 403 `authz.denied` without the permission, 404 `privacy.request.notfound` for
  an identifier naming no request, and 409 `privacy.request.decided` where a decision
  already stands.
- A host can bind one of its actions to the purpose it is done for, and where that
  purpose rests on consent the gate refuses the action until the data subject of the
  record being acted on has consented to it: missing, withdrawn, superseded or of the
  ordinary kind where the written one is required, each answered by the code that names
  what is wanted. A host says who that subject is when it registers a record, reading
  the column its resource type declares for its encrypted fields, and the gate reads
  that subject's consent for the purpose the action is done for. Staff, system and
  background callers are gated exactly as the subject's own request is, and a check that
  names no record, or a record naming no subject, is refused where the purpose rests on
  consent. A deployment binding a consent-based purpose to a type whose encrypted fields
  name no one subject column does not start. The capability carries `consent` as
  something the action still requires, so a control prompts rather than failing
  silently. A page reads the consents of every data subject on it in one query. Consent
  gates the purpose and not the record, so an action on the same record done for a
  purpose resting on another basis is untouched.
- The terms step of registration records one consent per control the person ticked,
  naming the purpose, the version presented of the document that governs its consent and
  the registration mechanism. A control left unticked records nothing and holds nothing
  up.
- A subject can read and change their own consents and objections through a privacy
  dashboard: `GET /privacy/consents`, `POST /privacy/consents/{purpose}/grant` and
  `.../withdraw`, `GET /privacy/objections`, `POST /privacy/objections/{purpose}` and
  `DELETE /privacy/objections/{purpose}`. Each record names the purpose, the version of
  the document that was shown, where the decision was made and when. A grant made on the
  subject's own pages records `dashboard`, and one answering the prompt a material
  revision raised, over a consent the revision ended and the subject never took back,
  records `reconsent`; a host granting through the contract names its own mechanism.
  Withdrawal takes the one request granting took and nothing stands in its way. A
  purpose that rests on a basis other than consent takes no consent record, and one
  whose basis carries no right to object refuses the objection by name. A purpose
  declaration names the legal document that governs its consent, and the privacy notice
  governs the purposes that name none. A consent is recorded against the version of that
  document, and publishing a material revision of it ends the live consents of the
  purposes that name it and of no others, so the subject is asked again, and leaves the
  records standing as evidence. A purpose declared on two types against two documents
  fails startup.
- The privacy notice and every other legal document a deployment publishes are served
  over `GET /privacy/notice` and `GET /privacy/documents/{document}`, public and without
  a sign-in, each answer carrying the governing language, the text that binds and every
  attached translation. A version is named in the query to read the text that was shown
  at the time.
- A deployment can publish its legal documents through the library: the privacy notice,
  the terms of service and anything else it holds. `POST /admin/notices` and
  `POST /admin/documents/{document}/versions` publish a version under `notice:publish`,
  with its governing text, its governing language and any translations, and a required
  `material`. Each version carries one governing language, defaulting to the one the
  deployment configured, and the text that binds in it. Translations attach to a
  published version and correct it without making a new one, through
  `PUT /admin/documents/{document}/versions/{version}/translations/{language}`, and a
  read returns the governing text together with every translation so a screen can show
  either without changing the interface language. A version submitted without its
  governing text does not publish, and the condition is raised for an operator to see.
- The error catalogue carries the privacy codes: a consent that is required, superseded
  or has to be written; a purpose whose basis carries no right to object; a document
  version submitted without its governing text; a duplicate request; a received date in
  the future; and an erasure that has not exhausted its retries. Each answers the status
  its endpoint states. A document or version that was never published answers 404
  `privacy.document.notfound`, a grant or withdrawal on a purpose that is undeclared or
  rests on another basis answers 422 `privacy.purpose.noconsent`, and a grant before any
  privacy notice has been published answers 409 `privacy.notice.unpublished`; none of
  the three is a permission refusal.
- Which capture path a consent runs through follows from the lawful basis and the
  sensitivity of the type: a consent-based purpose over sensitive data, on a basis that
  requires it, takes the written path with nothing further to configure. A deployment
  may state the written path itself and never the ordinary one where the written one is
  required, which startup refuses.
- A declared purpose carries the categories of data it requires and the categories of
  person it is about, and startup refuses a purpose that names no data category, so the
  declaration states what is collected rather than what happens to be held. A resource
  type declared sensitive in a category the deployment did not declare is refused at
  startup for the same reason.
- A deployment can register a callback that says what its gateway knows about a phone
  number. Before a sign-in link or a second-step code goes to a number, the callback is
  asked and the answer is written to the audit trail against the factor it was asked
  for; where no callback is registered the absence is recorded instead. A carrier
  reporting a recent change of SIM or of network withholds the entry a text would carry:
  the second-step challenge offers the account's other methods in its place, and a
  sign-in that has no other second step is refused with `auth.factor.rejected` rather
  than completing below what the account asked for. A sign-in link by text is the whole
  of a sign-in, so it is refused outright; the question is asked of the number and never
  of the account, so a number no account holds is refused in the same bytes.
- A credential offered for upgrade as a second-factor security key when it is not one
  answers with a code of its own, not the one a refused factor answers with, and a
  credential of another account answers as one that does not exist.
- A deployment that cannot reach its secrets manager stops as it starts, with the code
  that says which value was not there, rather than failing at the first request that
  would have read a person's field. Startup refuses a fingerprint key set without its
  current version, or with any version shorter than 32 bytes, as it refuses an absent
  one.
- What has expired is removable without anyone's attention: a session past its absolute
  expiry, an authorization code past its lifetime, a refresh token past the expiry its
  session gave it and a signing key past its overlap each go in one call.
- The deployment is an OpenID Connect provider for the clients it registers itself: it
  advertises what it answers, publishes the keys a relying party validates against,
  hands a browser that already holds a session a code without asking anyone anything,
  and exchanges that code over the back channel for tokens signed with a key that
  rotates on its own. The protocol library does the protocol's work throughout: it
  validates the clients, issues and rotates the codes and the tokens, proves the
  verifier and catches a reuse. What the library decides is what no protocol server can
  know: the session record every token is minted from, the kind of client, the one
  destination a code returns to, and the key that signs, which is the key the
  deployment's own store holds and publishes. Every authorization request is pushed
  first: a client posts its parameters to `POST /oidc/par`, authenticated with its
  secret, and sends the browser to `/oidc/authorize` with its `client_id` and the
  `request_uri` it was answered with. `/oidc/authorize` refuses a request that carries
  its parameters instead, with `invalid_request`; a `request_uri` is taken once,
  whatever it is answered with, and lapses 60 seconds after it is issued. The discovery
  document names `pushed_authorization_request_endpoint` and
  `require_pushed_authorization_requests`. A proof key is taken by S256 alone: a request
  naming the plain method, or carrying a challenge that names no method, is refused with
  `invalid_request`, and the discovery document does not list `plain`. Where a browser
  holding no session is sent to sign in is a declaration with no default, and a
  deployment that registers none does not start; `login_required` is the answer to
  `prompt=none` alone. A client registered as a browser application is handed no refresh
  token, and its own layer that asks for `offline_access` is refused where it asks, with
  `invalid_request`; a protocol client is handed one that rotates on use, and presenting
  a spent one ends the session everything stood on. An authorization request naming a
  client the registry does not hold is answered 400, and a destination that is not the
  client's registered one is replaced by it rather than refused. Every access token
  names the client it was issued to in `aud`, beside `client_id`, as RFC 9068 has it, so
  a party verifying a token offline can refuse one issued to any other client. The token
  and userinfo routes are carried on the machine profile, a second pipeline profile that
  reads no cookie and asks for no synchronizer token, and refuses a request that arrives
  with one. `IOidc` carries the two operations a host calls in process and the library
  answers over HTTP, `ClaimsAsync` and `KeysAsync`.
- An application establishes its own session from the one the authentication application
  holds without a line of host code: `GET /auth/signon` forwards the browser to the
  provider with proof key and a state bound to its pre-authentication session,
  `GET /auth/signon/return` trades the code on the server's own connection and drops
  what came back, and the browser goes on to where it was heading with a session of this
  application's own. The sign-on pushes its request on the back channel as it exchanges
  its code, so the browser carries nothing of the request, and the back-channel request
  is made with the client `identity-signon`, which a host configures as it configures
  any other client. A host declares the client identifier this application is registered
  under and hands the library the matching secret from its secrets manager, both of
  which startup requires; the address of the authentication application is declared
  beside its sign-in screen.
- The OpenID Connect provider keeps its registered clients and its signing keys in
  tables of the library's schema, and the protocol library keeps its own records in
  three more: `oidc_authorizations`, `oidc_tokens` and `oidc_scopes`. Codes and refresh
  tokens are encrypted under a key derived from the deployment's key-encryption key, so
  every instance reads what any other wrote, a restart loses nothing, and a key rotation
  leaves the ones already issued readable. A signing key's private half is wrapped under
  the deployment's key-encryption key.
- Every message the library sends goes down one path, and the named restrictions decide
  whether it goes: a fourth text message to one number inside a day is refused 429 with
  the time the restriction lifts, a message a transport would not take is not counted
  against anything, and a security notice to the holder of an existing address is not
  held back by a destination whose allowance is gone. A message goes out in the language
  its recipient's account settled on, else, where it answers a registration, sign-in or
  recovery request, in the locale that request carried, else in every language of
  `notification.languages`; a tag such as `en-GB` finds a declared `en`, and alerts take
  the same path. The restrictions judge a message in every language once, and each
  language is then a message of its own: announced, carried and counted under its own
  reference, so a failed delivery report releases that one alone and a mail restriction
  of one a minute refuses the next request rather than the second language.
  `SendRequest.Language` is nullable, and null means every declared language.
  Registration settles the account's language from the locale it was begun under.
  `IRecovery.ApproveAsync` takes no language, since the approver's locale says nothing
  of the person recovered. The holder of an address someone tried to register is told in
  the holder's language, and an invitation link goes out in every declared language.
  Support can add credit to one exhausted key, and a restriction can be created, changed
  or deleted while the deployment runs; both need step-up, a loosening needs a written
  reason, and both are audited without the key ever being written down.
- Every authentication, registration and recovery attempt waits out a progressive delay
  that grows with the failures counted against the source, the account and the
  identifier, and the delay is the same whether or not an account holds the identifier
  that was typed. An address no account holds is told so once per window, in a message
  that names no one, and a registration from a datacenter range or from a source that
  has just made many attempts is put behind the deployment's own challenge where one is
  registered.
- The operator is alerted when the conditions of the operations chapter fire, once per
  sustained attack rather than once per attempt, by email and, for the severe ones or
  where email reached nobody, by text message. Changing where those alerts go tells the
  previous destinations first. A condition is written in the transaction that raised it
  and carried after that transaction commits, oldest first and once, so one raised by an
  operation that then fails is never sent.
- A text message is refused before it is sent when the gateway balance is at the floor,
  alerts excepted, and a balance that is draining faster than it has been raises its own
  alert.
- The sending restrictions, the progressive delay, the alerting and the delivery reports
  are kept in tables of the library's schema. No plain address, account or source is in
  any of them: each is held under the deployment's fingerprint key. A send counter holds
  the keyed hash and the times and nothing else, and how long it is kept follows the
  restrictions as they stand rather than the interval the send was counted under: the
  read before every send takes with it every record whose newest time is older than the
  longest interval declared, so shortening an interval reaches the sends already
  counted.
- A deployment whose restriction names a key supplier nothing supplies, or whose
  `integration.mail.endpoint`, `integration.sms.endpoint` or
  `password.blocklist.selfhosted.address` is not an HTTPS address, fails to start,
  rather than at the moment someone is waiting for a code. A corpus address written
  over plain HTTP after startup is never asked; the offline list answers instead.
- The message catalogue, the mail and text transports, the recipients a deployment's
  data reaches and the keys a sending restriction counts under are the host's to
  declare, and each is optional: a deployment that declares none of them starts.
  Notification handling is a contract a deployment can replace: `INotificationHandler`
  in `Janus.Core` takes which message goes to which destination in which language, and
  the shipped handler, which renders the deployment's templates and hands them to the
  mail and SMS transports, is registered only if the deployment registers none of its
  own. The addresses the library itself calls out to are two protected configuration
  keys, `integration.mail.endpoint` and `integration.sms.endpoint`; a deployment that
  supplies its own mail or SMS transport leaves them empty and calls its provider
  wherever it decides, and a host's own outbound calls are the host's to check.
- Registering the library registers the sessions, passwords, factors, trusted browsers
  and policies of the authentication chapter as well, so a host resolves them from its
  own container. Passing the new-device check is announced as `DeviceVerified`, carrying
  the browser and nothing about the person.
- The audit entry for a restriction change carries what the restriction was and what it
  became, so an operator reading the trail sees the change and not only that one was
  made.
- `IConfigurationStore` in `Janus.Core.Configuration` reads every runtime-changeable
  configuration key from the library's own `settings` table, where the value lives
  rather than in a file, so a value changed anywhere in the deployment, through the
  management application included, is in force for the next read of it without a
  restart. A key the deployment never wrote reads as the default the catalogue gives it,
  a stored value a tightened floor or ceiling does not admit comes back as a failure
  naming the constraint, and a key the application may not change is refused whatever
  the caller asks. `WriteAsync` for one member of a key that exists once per
  organization or once per declared category puts the value in force for the next read,
  answers what was in force before, and refuses a protected family or a value the family
  does not admit.
- The browser-facing pipeline is mounted with one call, `UseBrowserProfile`, and
  protects whatever the host mounts after it: a cross-site state change, a state change
  without the `X-Identity-Request` header, one claiming another origin, and one whose
  synchronizer token is absent or belongs to another session are each refused before any
  endpoint runs, and no key, attribute or route excludes an endpoint from any of it. A
  frontend sends `X-Identity-Request` on every call and the synchronizer token in
  `X-Identity-Csrf`. A form another site posts as the whole page, with no session cookie
  on it, is answered 303 with its own address, so the browser reads that address with
  the session and the host's GET route there continues; nothing of the post is carried,
  and the same post carrying the session, or one that does not navigate the page, is
  refused. The session and its token are set in two cookies whose attributes no
  configuration can weaken, strict for the management application and lax elsewhere. The
  cookies the library sets are `__Host-identity-session`, `__Host-identity-preauth`,
  `__Host-identity-csrf`, `__Host-identity-browser` and `__Host-identity-device`. A
  session cookie that does not resolve does not refuse the request: the pipeline clears
  the cookie and carries the request on as anonymous, so a person whose session ended
  can reach the sign-in endpoints with the dead cookie still in the browser. The
  endpoints that answer only a signed-in person are held to a session in one stage,
  which answers 401 `auth.session.expired`, carrying what has to be done again where the
  session had ended.
- Every request body is read through the library's generated serialization contexts and
  through nothing else. A request the library cannot read is answered with the same body
  as every other refusal: `api.request.malformed`, a correlation identifier, and a
  `details.member` naming the member the reader stopped at or the one the endpoint
  required, so a caller traces it as it traces any other.
- The browser profile admits each source address `abuse.source.ratelimit` requests a
  minute (300 by default, sliding) and answers the rest 429 `auth.throttled` with
  `retryAt`, before any session is looked up. Each instance of a deployment counts on
  its own, by the connection address after the proxies the host trusts, so a
  deployment of several instances sets the key to each one's share.
- Capabilities never list a permission the authorization model does not declare,
  whatever a stored role still allows.
- A host's restriction key supplier is asked once for each key name when a send is
  judged, however many restrictions count under that key.
- Every throttled answer (sign-in, sign-in link and email code, recovery, break-glass
  and the recovery approval limits) carries `retryAt` in its details and a matching
  `Retry-After` header. The progressive delay runs from the failure that earned it and
  grows when failures follow one another, so `retryAt` is the instant the next attempt
  is looked at.
- `auth.stepup.required` carries `required` (`level`, `phishingResistant`, `maxAge` in
  seconds), `outcome`, `options` and `pendingUntil` on every gated operation, as the API
  contract gives them, and nothing else.
- Factors presented to step a session up are held by the progressive sign-in delay,
  counted against the same source and account as sign-in failures;
  `IAuthentication.StepUpAsync` takes the request's source address.
- A wrong device verification code, a pressed sign-in link that lands on no sign-in, a
  refused delegated or provider sign-in and an unknown sign-in challenge are recorded as
  `auth.authentication.failed` and held by the progressive delay.
- A refused sign-in factor counts against the identifier as typed, whether or not an
  account holds it, so a held and an unheld address are delayed alike from any source;
  a success clears only the account's count. Only a remembered or trusted browser token
  that resolves to the account exempts a browser, through `IAuthentication.BeginAsync`,
  and never from the source's delay. A throttled source reaches no sign-in provider.
- A sign-in link, email code or recovery ask that sends nothing is judged and counted
  against the sending restrictions as its message would be, so it is answered as a sent
  one is, whether or not an account holds the address.
- Startup refuses a purpose named for the hosting or its cross-border transfer
  (`hosting`, `transfer`, `hosting-transfer`, `cross-border-transfer`) that rests on a
  consent basis, with `model.startup.declarationmissing`.
- A runtime setting changed in process is refused without a reason,
  `auth.restriction.reasonrequired` naming the key, whichever way it moves, as over
  HTTP; the named restriction set asks a reason of a loosening only.
- Every endpoint the library mounts calls its operation through a public service
  contract. `ICredentials.LinkableAsync` and `ICredentials.UnlinkAsync` check and unlink
  an identity at a social provider in process, under the same checks as `POST` and
  `DELETE /account/link/{provider}`. `IMaintenanceRecords` reads and replaces the
  licences and permits and reads and appends the maintenance log in process, under
  `compliance:manage`, and `Licence`, `LicenceId`, `LicenceKind`, `MaintenanceEntry`,
  `MaintenanceEntryId` and `MaintenanceTask` are public. `IRegistration.BeginAsync`
  takes the access context of a browser already signed in: it creates no registration
  session for it, attaches the invitation its link carried to that account, and answers
  `identity.registration.signedin`.
- An organization's name is judged on its comparison key, the `NFKC_Casefold` form every
  identifier is compared under, stored beside it in `organizations.canonical_name`; a
  name mixing scripts within a word is refused as `identity.identifier.mixedscript`, and
  one the key reduces to nothing as `api.request.malformed`.
- A management or account request missing a member its body requires is refused before
  anything else is judged: a missing reason by its own code
  (`authz.grant.reasonrequired`, `auth.restriction.reasonrequired`,
  `auth.recovery.reasonrequired`), any other member as `api.request.malformed` naming it.
- `bootstrap` requires the first administrator's date of birth as `--dateofbirth`
  (`yyyy-MM-dd`) and refuses an administrator under eighteen as
  `identity.profile.underage` before anything is written, where
  `registration.adultaffirmation` is `required`; the emergency account and the restore
  canary record no answer.
- Finding who holds access to a record reads the live grants on its ancestors through
  their index, and `organization_domains.domain` carries the `identity_ci` collation.
- A value the library reads from text under a rule, left unset (such as its `default`),
  throws `InvalidOperationException` where its text is read, so no such value reaches a
  row.
- Under the mount, a path no endpoint serves and a method a path does not take answer
  404 `authz.resource.notfound` in the error envelope, and a fault answers 500
  `system.fault` with the correlation identifier and nothing of what was thrown; the
  log keeps the fault's type under that identifier. The host's routes outside the
  mount answer as the host has them answer.
- An authorization request refused where the refusal cannot go back to a client is
  answered to the browser in the error envelope rather than as the provider's text:
  400 `api.request.malformed` with the protocol's code in `details.error`, or 500
  `system.fault`.
- Every session carries a synchronizer token of its own, bound to that session and to no
  other, and reissued whenever the session's secret is. Neither value is ever read back:
  the record holds only what each fingerprints to.
- Every password is screened against the compromised-password corpus before it is
  accepted, over the range API: only a five-character hash prefix leaves the deployment
  and the range that comes back is matched locally, so neither the password nor its full
  hash is ever sent. Naming the self-hosted corpus is the whole of switching to it: it
  is reached at the address `password.blocklist.selfhosted.address` names, over the same
  range protocol the primary source uses, and a deployment that names `selfHosted` and
  no address does not start. The offline leaked-password list travels in the package and
  is refreshed with each release rather than by the operator, so a deployment that holds
  no corpus file of its own falls back to a dated list when the range API cannot answer.
  A fall back from the configured corpus to the offline one is raised as `degradation`
  under the scope `password.blocklist.fallback`, naming both corpora. Where the alert
  cannot be raised, where neither corpus can answer, or where the corpus is older than
  the deployment admits, the password is refused rather than accepted unscreened. The
  directory beside the application that holds the word lists is `identity-corpus`.
- Sessions, passwords, enrolled credentials, recovery codes and known browsers are
  stored in PostgreSQL. A session's record carries what it reached and the fingerprint
  of its cookie, never the cookie, and where it was used from is held under the person's
  key, so a database dump yields no location and erasure leaves none readable. A code
  generator's shared secret is held under the same key, and a recovery code is stored as
  a password is.
- A second step is second to a password: a code generator or a security key under
  two-step is refused to an account that holds none, and an account signing in with a
  passkey alone is offered no second step to enrol. A set of recovery codes is not a
  second step and stays available either way.
- A password is set and presented through one path: the length floor that the account's
  reachable assurance decides, the screening that never silently skips, the hashing, and
  the silent rehash on the next sign-in after the parameters are raised. A password that
  matches one of the person's own words only after it was set carries a prompt to change
  into the sign-in that completes.
- Raising the assurance floor or the redundancy requirement of a policy, the system's or
  an organization's, gives the accounts already under it a run-up of
  `policy.enforcement.grace`: an account that does not meet the new requirement signs in
  until the date it falls due, each sign-in carrying the requirement and that date, and
  stops at enrolment afterwards. A run-up of nothing holds such accounts at once, an
  account created after the raise was created under the new requirement and is held at
  enrolment at its first sign-in, and lowering a requirement starts no run-up.
- The relying party a passkey is bound to is settled when the deployment starts, not at
  the first enrolment: an identifier that does not sit over every configured origin
  stops the deployment, and one left unset is derived as the parent domain the origins
  share rather than taken from the first of them. The related-origins document lists
  exactly the additional origins configured, and a set of them wider than a browser
  reads stops the deployment too.
- A browser can be trusted after a two-factor sign-in, which spares it the second factor
  and nothing else: the session that follows records only the password, the offer is
  absent where the policy requires two factors or the password is too short to stand
  alone, and the trust goes when it lapses, when the account signs out everywhere, when
  the person removes it from their device list, or after enough failed sign-ins on it in
  a row.
- A sign-in to an account that can reach only one factor, from a browser the account has
  not been seen on, is held: a code goes to the account's primary email, and the sign-in
  completes when that code is typed. The browser is then remembered and not held again
  for as long as the deployment says. A passkey sign-in and a sign-in that reached two
  factors are never held this way, and the check can be turned off. A verification code
  is an aggregate with a table of its own: it lives `code.verification.lifetime`
  whatever issued it, dies on the try that reaches `code.verification.attempts`, and is
  spent by the first right one. The new-device check issues and answers through it, and
  a sign-in in progress carries no code and no count of wrong ones.
- A step-up gate is answered from the session record and what the account can reach with
  the credentials it holds, and never from a list of what it has enrolled. Where the
  session falls short, every combination of the account's own factors that would reach
  the gate is offered and the person chooses among them; where none would, the answer is
  to enrol, to report the loss, or that the loss report already made completes at a
  stated time. A reported loss lowers what an account reaches only once its window has
  run, an emergency session passes every gate while it lasts, and enrolling a credential
  costs the lower of what the account reaches and what the new credential itself would
  contribute.
- A WebAuthn credential records the relying party it was created under, whether it may
  be synced and whether it currently is, so a credential left behind by a configuration
  change is found from the account's own record rather than at a sign-in that fails
  without explanation. Enrolment refuses an algorithm outside the configured allow-list
  and a ceremony that verified nobody, asks for no attestation, and a signature counter
  that fails to advance is refused and audited as the cloned credential it indicates. A
  second-factor security key can be re-registered as a passkey, which retires the entry
  it came from. A creation ceremony carries who the credential is for: the handle is the
  account's subject identifier, the name is its primary email and the display name is
  what the account shows or empty, so an authenticator can offer the credential back
  unprompted. An assertion that returns a handle naming another account, or one the
  library never issued, is refused as a wrong credential is.
- Recovery codes are issued ten at a time, each ten symbols of an alphabet that omits
  the letters a reader confuses with digits, and are read back leniently: case, spacing
  and those confusions make no difference to whether a code is accepted. Only hashes are
  stored, a code is spent on first use, and generating a set retires the previous one
  whole. The account shows how many remain. A set older than `recovery.codes.reminder`
  reminds its owner, once, on every channel of the security-notice set, under the
  message kind `recovery-codes-reminder`; a daily job sends it to active accounts only.
  The account read shows `recoveryCodes.remindedAt`, and the export carries it.
- Time-based codes run on thirty-second steps at six digits, with a drift tolerance the
  deployment sets and no code accepted twice: a code whose step has been spent is
  refused as replayed, so an observer has no window to reuse one in. An enrolment
  becomes usable only once a valid code has been presented, so a mis-scanned secret
  locks nobody out, and an enrolment abandoned before that leaves nothing behind.
- A session is a server-side record every credential derives from, holding the
  properties an authentication reached and never the factor names that reached them.
  Ending the record ends the per-application sessions and the tokens standing on it. The
  browser carries thirty-two drawn bytes and the row holds their fingerprint, so a dump
  of the table yields no usable session.
- Session lifetimes follow the assurance the principal's policy requires and never the
  level a particular sign-in happened to reach, so a customer who signs in with a
  passkey keeps the customer lifetimes. A new secret is issued whenever a combination is
  presented and on any privilege change, and the one before it stops working.
- After an inactivity expiry inside the absolute window, a policy that requires two
  factors accepts one factor bound to the session secret the browser still holds. The
  allowance is not offered under the system policy, and a second factor alone, a social
  credential and an email factor each restore nothing.
- Authentication policy is resolved from the principal's organization membership and
  from nothing else, read from the database so that it resolves against live memberships
  and not against ended ones: the system policy where there is no membership, the
  organization's where there is one, and the strictest of several where a principal
  belongs to more than one organization. An organization may tighten any field and a
  value that would loosen one below the system default has no effect, whenever it was
  written.
- A sign-in link, whichever channel carries it, and an emailed code sign a person in at
  AAL1 and count for nothing afterwards: neither is a second step, neither passes a
  step-up gate, and neither restores a session that lapsed. The mailbox or the number
  behind them is also the recovery channel, so one compromise would otherwise yield both
  steps.
- Passwords are held under Argon2id at the parameters the deployment configures, with
  the parameters carried by each hash, so raising them leaves every stored password
  verifiable and marks it for a silent rehash. The floor is fifteen characters where the
  password could sign in alone and ten where it never could, and the shorter floor is
  reached by holding a second factor rather than by choosing it. There are no
  composition rules and no scheduled expiry, and a password longer than
  `password.maximum` is refused with `auth.password.toolong`, never with a configuration
  code.
- `ISessions` in `Janus.Core`: an account sees its live sessions with the time of
  sign-in, the time of last use, the device and a city-level location, ends one of them
  on its own, or signs out everywhere. An administrator ends one account's sessions, and
  the emergency operation ends every session in the deployment.
- `IAccessGate` in `Janus.Core`: the one place a permission is evaluated. A check and a
  list filter are the same rule rendered two ways, an expression a host composes into
  its own LINQ query and a parameterised PostgreSQL fragment a hand-written query
  composes into its `WHERE` clause, so a list screen cannot come to show what a check
  would refuse. Neither rendering enumerates permitted records.
- `MapAuthorizationTables` on `AuthorizationTables` in `Janus.Hosting`: a host maps
  `AncestryEntry` and `EffectiveGrant` of `Janus.Core`, the ancestry closure and the
  effective grants, into its own context, so a filtered listing is one query against its
  own tables and the library reads nothing of the host's.
- `AddJanus` on `HostingRegistration` in `Janus.Hosting`: the one method a host calls to
  register the library. It takes the database connection, the key-encryption keys, the
  fingerprint keys, the sign-on secret, the maintenance credential, the host's
  declaration and the `ApplicationKind` the pipeline is mounted in. The maintenance
  credential is the database connection of a login holding the maintenance role's
  rights, read from the secrets manager; a deployment that supplies none does not start,
  with `model.startup.kekunavailable` naming `maintenanceCredential`. What the host
  declares about its own domain is built and checked here, at startup. The endpoints
  mount with `MapIdentityEndpoints` and `MapIdentityWellKnown` on `IdentityEndpoints`,
  and the two profiles with `UseBrowserProfile` and `UseMachineProfile` on
  `PipelineProfiles`. Only the namespaces, the package identifiers and `AddJanus` carry
  the product's name.
- The gate explains itself: an explanation names the grant that decided, the container
  it was inherited from, and the principal it was decided for, or states that no grant
  matched. An explanation can be asked with the host's own rows, and on a type a
  derivation reaches it names the grant the fact produced: no identifier, the derived
  kind, the role the derivation confers, and the container it was inherited from. The
  identifier an explained grant carries is optional for that reason: a derived grant is
  a fact being true and no row holds it. A page of capabilities costs one query over the
  host's own rows however many permissions it asks for: every derivation reaching the
  type is evaluated in that one query, and what the role each confers allows is read
  from the model, so a page that offers three actions costs what a page offering one
  costs.
- A refusal on one record answers as a record that does not exist unless the type says
  otherwise, and a type says so in one place for every record of it. A type that
  conceals has no self-service explanation, because saying that no grant matched says
  that the record is there. The browser profile answers such a refusal as `404
  authz.resource.notfound` carrying the identifier it was recorded under, whatever the
  endpoint wrote after it, and the answer is the same whether or not the record exists.
- Every refusal carries a correlation identifier, whatever the request was made under,
  background work included, which is the audit row it was recorded as. A refusal of work
  done under neither identity is recorded with neither named, which is the recorded fact
  rather than an omission, and the database refuses any other kind of event that names
  neither. A caller holding `audit:read` in the administrative organization resolves the
  identifier to the permission and the principal, whichever organization the refusal was
  recorded in; it says nothing about whether the record exists. A permission that names
  no record is refused as a permission the caller does not hold, with nothing concealed.
- A check, a filter or a fragment naming a resource type the model does not declare
  raises at the request, before the caller's restriction is read or anything is
  recorded, so the calling code's fault is the same whoever asks.
- A refusal is recorded with the grant that decided it, where one did, so its correlation
  identifier resolves to what the gate explained at the time: a deny grant is named with
  the container it sat on, rather than the refusal reading as one no grant matched.
- A refusal the library answers is logged under the correlation identifier the answer
  carries, by its code. A fault is logged at error with the code and the structured
  context the answer withholds, so a `system.fault` is traced to its cause by that
  identifier alone.
- The audit trail records a factor refused at sign-in, or a refused break-glass code, as
  `auth.authentication.failed`, and a factor refused at a step-up as
  `auth.stepup.failed`, whatever the host's log level. Each record names the factor, and
  the account the attempt was made against where there was one, and nothing that was
  typed.
- Inheritance is resolved through an ancestry closure maintained in the same transaction
  as the create or the move that changes it, so a permission query joins one table
  rather than walking the tree, and permission data and business data cannot diverge.
  Moving a record carries everything beneath it.
- Groups nest to any depth, and the groups a principal belongs to are read once per
  request from a closure maintained beside the memberships. Adding or removing a member,
  or writing a grant to a group, raises the counter of every account it reaches, in the
  same transaction.
- A grant is one sentence, subject has role on resource, and one row carries every kind
  of it: an account's and a group's, an allow and a deny, a grant on one record and a
  grant on a whole organization. What a role allows is read where a grant naming it is
  evaluated, so editing a role takes effect at once.
- Every grant records who granted it, when and why, and a revocation records the same. A
  grant or a revocation stating no reason is refused with `authz.grant.reasonrequired`.
  An expired grant confers nothing at the instant it is read, whether or not a sweep has
  run.
- A check, a capability page and an explanation take the same host-supplied rows the
  filter takes, so a type whose access follows in part from a fact in the host's own
  data answers the same way whichever of them is asked. Asked without those rows on a
  type a derivation reaches, they refuse with `authz.derivation.sourcesmissing`, a fault
  and not a denial, whatever that derivation confers, rather than answering from the
  stored grants alone, so a call site that passes cannot start faulting because an
  administrator edited a role.
- A derivation confers a role from a fact in the host's own data. The host declares the
  relationship and hands its rows to the filter beside the ancestry and the grants, and
  a listing then reaches everything that fact reaches, on the record or on anything
  containing it, with no grant written and nothing to keep in sync. Removing the fact
  removes the access on the next request, and a deny defeats a derived grant as it
  defeats a written one.
- `IDerivationMaterialiser` in `Janus.Core`: a derivation a deployment declares
  materialised is precomputed into ordinary grant rows, marked as such wherever they are
  read. The host refreshes it from the operation that changes the relationship, inside
  the same unit of work, so the fact and the rows computed from it are written together
  or not at all. A refresh run later reports what it had to change and corrects it in
  the same run, which is how drift is found where the refresh was missed.
  `derivation.materialised.driftcheck` sets how often that runs.
- `AccessContext` in `Janus.Core`: who is acting, whom they are acting for, and the
  named principal a background job runs as.
- `GrantId`, `GroupId`, `GrantSubject` and `ResourceReference` in `Janus.Core`: what a
  grant, a group and one of the host's records are named by. A record's identifier is
  the host's own text, so an integer, a UUID or a code all serve.
- `AuthorizationDeclarationBuilder` in `Janus.Core`: a host declares its own kinds of
  thing, their containment, what they are processed for, which fields are encrypted and
  under whose key, and the relationships in its own data that confer a role. Every
  reference to one of the host's fields is an expression the compiler checks.
- The authorization model is built and checked once, at startup: a containment cycle, a
  reference to a type that was never declared, a type that reaches no organization, a
  type with no purpose, a purpose whose basis needs an assessment and names none, and a
  derivation from a relationship that was never declared each stop the deployment with
  their own code.
- The two checks the declaration alone cannot decide run as the deployment starts and
  before it serves a request: a role someone wrote allowing a permission the model does
  not declare, and a derivation naming a column no index reaches, each stop the process
  with their own code.
- The built model is written to `model.json` in one order, so two runs of one
  configuration produce the same bytes and a change to the model is a diff in review. It
  lists what the maintenance credential may reach, the two audit partition functions it
  may execute and the rights it holds on the wrapped keys, so a reviewer reads them in
  `artifacts/model.json` beside the rest of the model as well as in the migration that
  grants them.
- `Permission` and `Permissions` in `Janus.Core`: a permission is a lowercase
  `resource:action` that cannot be constructed in another shape, and the library's own
  twenty-two are listed where a host can read them.
- `SubjectType`, `GrantKind` and `ConcealmentBehaviour` in `Janus.Core`: what a grant is
  held by, where it came from, and what a denial on a record discloses.
- The authorization error codes: a denial, the four grant refusals, a group that would
  contain itself, an entity with no registered policy, a restricted subject, and the
  three model validations a startup fails on.
- `JAN0006`: a caught exception that is neither handled nor reported fails the build.
- `Result`, `Result<T>`, `Error` and `ErrorCode` in `Janus.Core`: an expected outcome is
  handled through `Match` or `Switch` and carries a code from the catalogue.
- `NeverLoggedAttribute` in `Janus.Core`: a value marked with it cannot be passed to a
  logging call. Every public type and member that carries a password, a token, a code, a
  key or the text of a notice is marked with it: `SessionId`, `GeneratedRecoveryCodes`,
  `KeyEncryptionKeys`, the code of `LinkLanding` and `SignInLanding`, the token of
  `IssuedInvitation`, the secret and address of `GeneratorEnrolment`, the text of
  `DocumentVersion` and `DocumentTranslation`, and the secret parameters of the service
  contracts. The marker states the rule for the host as it does for the library's own
  build.
- `Settings` in `Janus.Core.Configuration`: every configuration key of chapter 10
  section 4 with its type, its default, the floors, ceilings and value sets it admits,
  and the two rules that hold over a pair of keys rather than over one. Among them are
  the alert thresholds, the per-source flood limit, the outbox schedule and retry, the
  sweep interval, the message languages and sending domain, the calendar time zone, the
  domain re-verification interval, the location database cadence, the restore-test
  objective and interval, the two identifier maximums, the two size caps and the
  bot-defence signal set. A duration written in years or months is held at 366 days a
  year and 31 days a month, so a retention floor stated in years is never shorter than
  the calendar span it names.
- `StartupException` in `Janus.Core`: a deployment that omits a value it has to name, or
  names one outside its key's bounds, fails at startup rather than at the first request
  that needs the value. A deployment that has not named every key it has to name stops
  as it starts, before any other startup check and before the web server: the governing
  language under `model.startup.governinglanguage`, every other key under
  `model.startup.declarationmissing` with `details.key`. `hosting.environment` is among
  them in every deployment, since every deployment serves the records of processing.
  `ThrowIfIncomplete` takes the screening sources and whether the records of processing
  are generated, because `service.name` and `hosting.environment` are named only by a
  deployment that uses them.
- `Policy`, `Policies` and the step-up, factor and assurance vocabularies in
  `Janus.Core`: the policy a principal resolves to, with the system and administrative
  defaults. A policy refuses an assurance floor outside `aal1` and `aal2`, and refuses
  the emergency credential as a login factor. A policy's gate is written with `level`,
  `phishingResistant` and `maxAge`, the form `GET|PUT /admin/config/policy.default`
  carries and the settings table stores.
- The general codes of the error catalogue: a configuration value outside its key's set,
  or of the wrong type (`config.value.notallowed`), a missing deployment declaration, a
  model reference to something the host never declared, the three preference refusals, a
  required challenge, a grant with no reason, the last alert destination, and an
  unhandled fault.
- `CapabilityResidual`, `ConsentMechanism`, `AgeGroup`, `AlertCondition` and
  `BotDefenceSignal` in `Janus.Core`: what still stands between a principal and an
  action, where a consent record was made, what the age screen recorded, which condition
  raised an alert, and what bot defence counts.
- `SubjectId` and `OrganizationId` in `Janus.Core`: an account's opaque identifier,
  drawn from randomness alone so that it carries nothing about the person, and the
  organization's, ordered by the instant it was issued.
- `AccountState`, `SuspensionOrigin`, `DeletionOrigin`, `TakedownTrigger`,
  `ErasureStatus` and `ErasureReason` in `Janus.Core`: the state an account is in, why
  it entered the one it is in, and how far an erasure's host-side work has got.
- `ISecretSource`, `KeyEncryptionKeys` and `FingerprintKeys` in `Janus.Core`: the host
  supplies the key-encryption key, the fingerprint key and the maintenance credential,
  and the library ships no secrets-manager client and no default.
  `ISecretSource.ReadFingerprintKeysAsync` and `AddJanus` take a `FingerprintKeys`: the
  current version and every version still held.
- `IUnitOfWork` in `Janus.Core`: an operation runs in one transaction and commits once,
  and hand-written SQL takes its connection from the one accessor, so a query written by
  hand runs on the same connection and inside the same transaction as the rest of the
  operation and can never miss a write the operation has already made. An operation that
  fails part way through leaves nothing written.
- Per-subject encryption of personal fields: one data key per subject, wrapped in the
  database under the deployment's key-encryption key, with every value bound to the
  subject, table and column it was written to, so a value moved elsewhere does not
  decrypt. A subject key carries its re-wrapping to the row: a rotation of the
  key-encryption key changes the wrapping and no stored value. Erasure overwrites the
  wrapped key and everything encrypted under it becomes unreadable at once, wherever
  that field is held, including values in the host's own tables.
- Keyed fingerprints for searchable identifiers, computed under a key held outside the
  database, neutralised by erasure and never matched once neutralised. The fingerprint
  key has versions, as the key-encryption key has: every fingerprint is written under
  the current version and found under any version held, and the key document piped to
  the command line names `fingerprintKeys` with `current` and `versions`.
- Account states and the transitions between them: an account is created active,
  deactivated by its owner or suspended by an administrator, restricted at the subject's
  request, and removed only through a grace window it can be brought back from. A
  takedown passes through suspension into that window in one step and is reversed, never
  cancelled. The record itself is never deleted, so an audit trail keeps resolving after
  the personal data is gone.
- The library's own database schema, `identity`, with a migration history table of its
  own, `__migrations_history`, so a host's migrations never collide with it. The
  case-insensitive ICU collation identifiers are compared under, `identity_ci`, is
  created in that schema like everything else of the library's, so nothing of the
  library's can collide with an object a host holds in `public`, and a column names it
  by that schema. Every fixed vocabulary is stored as the spelling it carries on the
  wire and constrained by a check rather than a native enum type, so the admitted set
  changes without a locking migration.
- The package carries its own Unicode tables, at a pinned version. Composition,
  decomposition, case folding, the PRECIS properties and the script data all come from
  those tables rather than from whichever library the machine happens to have, so a
  canonical form computed on one host is the canonical form computed on every other, and
  the version the fingerprints were derived under is recorded.
- `CanonicalForm`, `Precis` and `ScriptMixing` in `Janus.Core`: the canonical form an
  identifier is stored and compared under, the digit mapping a phone number takes
  instead, the two PRECIS profiles a username and a display name must satisfy, and the
  mixed-script rule that holds within a word. Two addresses that differ only in how they
  are composed, in width or in case are one account, and a word that mixes scripts is
  refused while whole-word Arabic beside whole-word Latin is not.
- `IdentifierKind`, `IdentifierId`, `EmailAddress`, `PhoneNumber` and `Username` in
  `Janus.Core`: the three kinds of identifier an account holds, and the forms each is
  stored and compared under. An address, a number or a username that the rules of its
  kind do not admit cannot be constructed, so it never reaches a row.
- An account's identifiers are modelled: each one keeps both the form the person entered
  and the form it is compared under, exactly one identifier of a kind is primary once
  the account has a verified one of that kind, and each kind's backup setting decides
  who a security notice reaches beyond the primary.
- An account and its per-subject data key are written and read back through ports of
  their own, so the account a caller holds carries the transitions and the row carries
  the columns, and neither knows the other's shape.
- An account's identifiers and each kind's backup setting are held in the schema and
  written and read back through a port of their own. Both forms of an identifier are
  encrypted under the subject's own key; what is looked up is the keyed fingerprint of
  the canonical form under the deployment's fingerprint key, and the Unicode version
  that form was computed under is recorded beside it. A lookup by value matches on that
  fingerprint, so an address entered in another casing finds the account that already
  holds it. One live fingerprint of a kind exists across the deployment, so an
  identifier belongs to at most one account; erasure neutralises the fingerprint and
  leaves the row, and a neutralised fingerprint finds nobody and is outside that rule.
  Reading an account's identifiers unwraps its key once however many columns it
  decrypts, and reading them after erasure refuses rather than yielding anything.
- `IdentifierKinds` in `Janus.Core`: one field takes every identifier and the kind is
  read from the value. An address carries the sign, a number is digits once the
  separators a person writes are taken out, in whichever script they were typed, and
  anything else is a username where the deployment admits one. A username holds at least
  one letter, so that no value is both a number and a username, and an all-digit choice
  is refused with `identity.username.invalid`.
- `DisplayName` and `LegalName` in `Janus.Core`: the two names of a profile, each in the
  form it is held in. A display name takes the Nickname profile and is bounded in bytes;
  a legal name takes Normalization Form C and is bounded in scalar values; both are of
  one script per word.
- The schema carries an account's profile, and the profile is written and read back
  through a port of its own. The display name, the legal name and the date of birth are
  each held under the subject's own key, so a dump yields none of them and erasure
  leaves none of them readable; a field the account gives up clears its column, and a
  field it did not touch is not written again.
- An account carries a language, a time zone and the values of the preference keys the
  host declares at startup. A declaration names a type (`PreferenceKind.String`,
  `Boolean`, `Integer` or `Enum`), a default and whether only an administrator may set
  the key; a malformed one fails startup with `model.startup.preferencedeclaration`. The
  declared values are stored under the subject key as one document, capped by
  `preferences.maxsize`; the language and the time zone are not, so a notice still
  reaches an erased account in a language it reads.
- Organizations and memberships. An organization is an entity in the one identity pool
  and no isolation boundary; its deletion suspends it at once, runs for
  `organization.deletion.grace` and is cancellable until the window closes, after which
  the row stays and the identifier goes on resolving. A membership is a record of its
  own that ends without touching either side. An account holds one membership unless the
  deployment enables `organization.multiplememberships`: a second one answers
  `identity.membership.limitreached` and nothing is written, and enabling the setting
  admits it, with no migration and no deploy. A membership the account ended leaves room
  for another, and a second membership of an organization the account is already a
  member of is refused whatever the setting says, naming that organization. Nothing in
  the schema separates staff from customers.
- An audit trail. Every record names who acted, whose identity the action was taken
  under, the instant it occurred and the organization where one applies; an event about
  a principal holding no membership carries none, and the absence is the recorded fact.
  What happened is a code from one closed catalogue, `AuditActions`, and never a
  sentence; a contract test fails on an action added or respelled without the catalogue
  saying so, as it does for the error codes. The table is partitioned by retention
  category and then by calendar month, three months kept open ahead by
  `audit_ensure_partitions`, and an attribute an event must record is held under the
  subject's own key, so erasure reaches it without a row being touched. A record is
  written through the operation's own connection: inside a transaction it commits with
  the action, and outside one it stands alone.
- Named principals for background jobs, imports and webhooks. One acts for a single
  organization and reaches no other; the other exists for pool-wide work, runs only the
  operations it names and acts for nobody. Neither can be constructed without a stated
  reason.
- The erasure, as one operation and one transaction. The account reaches `deleted`, the
  subject's wrapped key is overwritten with the irreversible value, its fingerprints are
  neutralised, and an erasures row records why and how far the host-side work has got.
  Every session the subject holds ends before the key their fields are under is
  destroyed, so no request survives on a session whose account is gone. The photo row
  stays where it is and its bytes stop being readable with everything else the key
  covered. A transaction that does not commit leaves no row and erases nothing; there is
  no third state. Erasure progress is on that row and on no column of the account, and
  every outstanding erasure is read in one query.
- The administrative organization is marked on its own row, set once when the deployment
  is bootstrapped and by nothing else, and the database holds the mark to exactly one
  organization. Requesting its deletion is refused with
  `identity.organization.protected`; every other organization takes the deletion window.
  An administrative operation on the deployment or on an account (session revocation,
  recovery approval, the privacy request queue, records of processing, compliance text
  and the takedown) is permitted only where the caller holds its permission in the
  administrative organization; a grant in any other organization does not reach it, and
  before bootstrap has marked an organization administrative every such operation is
  refused.
- The three database roles the deployment attaches credentials to. `identity_migrate`
  owns the schema and is the only role that alters it, `identity_app` reads and writes
  rows, and `identity_maintenance` executes the two audit partition functions and reads
  and updates the wrapped keys. The migration creates the two runtime roles where they
  are absent and writes every grant, so an audit row cannot be updated or deleted by the
  application at all, and no credential that alters schema reaches the running system.
- `audit_drop_expired_partitions`: the scheduled job drops a month of one retention
  category once its end has passed that category's retention. The two retention periods
  are passed in, because a key left at its default has no stored row the database could
  read, and either below the floor its key carries is refused.
- A host binds one of its actions to a step-up gate in the model builder, and the gate
  is then read wherever the action is: a capability for it carries `stepup` beside what
  the grants confer, and a check of it is refused until the session satisfies the gate.
  The gate is judged against the acting person's own session: a gate named in the
  step-up catalogue costs what the person's policy states for it, and a gate the host
  names costs what the dearest gate of that policy costs. The list filter and the SQL
  fragment ask the bound gate as the single check does, and a refusal carries what the
  gate costs and what the person can present. A deployment that registers no assurance
  provider is refused with `auth.stepup.unavailable`, told apart from an ordinary
  denial.
- An account under a processing restriction keeps its reading actions and is refused
  every action that would change anything, with `authz.restricted`, in a check, a
  listing filter and a capability alike. `read`, `list` and `export` are reading by
  name, a host declares which of its own actions are reading, and everything else
  modifies. Its own settings are held the same way: an edit of its profile, its photo or
  its preferences, and a change to one of its identifiers, its credentials or its
  preferred second step, is refused with `authz.restricted`.
- Registration is served end to end. A browser that reaches the library is given a
  pre-authentication session, and the registration it starts is bound to that session
  and reachable from no other browser: the age screen, the email and phone steps, the
  confirm screen, the security step and the terms step, each refusing to run before the
  one before it has finished. An address or a number that already belongs to somebody
  else is answered exactly as a fresh one is, and its holder is told once that somebody
  tried. A registration that is abandoned leaves nothing behind. A browser that already
  holds a session and asks to register is refused with 409
  `identity.registration.signedin`; nothing is staged for it, and the frontend navigates
  to the account application.
- An identifier is verified by the code in the message or by pressing the link. The
  press verifies only in the browser that asked for the message; opened anywhere else
  the same request changes nothing and hands back the code to type, and a control there
  ends the attempt. Merely loading the link, which is what a mail scanner does, changes
  nothing at all. A waiting screen follows the state on a stream of server-sent events
  carrying exactly what the polling endpoint answers, so a frontend that loses the
  stream misses nothing. The stream is woken by the database: the transaction that
  verifies or completes a registration step announces the session on a PostgreSQL
  channel, and every instance holding a stream open for it hears the announcement. The
  stream also reads the state back every `registration.events.pollinterval`, so a
  deployment that cannot hear the channel loses promptness and never an event.
- An account reads and changes itself: its identifiers, its credentials and their
  labels, its profile, its preferences and its sessions. An identifier can be added up
  to the deployment's maximum, made primary, set as the backup destination, removed with
  an undo the remaining addresses are sent, and, where only one of a kind is allowed,
  replaced in one operation. A removed identifier stays out of reach of every other
  account until its undo window closes. Once a replacement applies every other session
  of the account ends, as a removal ends them, and the session that removes an
  identifier or completes the verification of one is given a new secret, the one before
  it answering nothing. The session list marks the one asking and says no more about
  where each was used than the city.
- A profile field the deployment has switched off is neither accepted from a request nor
  carried in an answer, and the date of birth is never the person's to change. A
  preference key the host never declared is refused and never returned. A username, once
  chosen, is held against every other account for the cooling-off period after it is
  given up, and after erasure for the same period. A username with a word that mixes
  scripts is refused with `identity.identifier.mixedscript`, as a display name is.
- The library serves `/.well-known/change-password`, `/.well-known/passkey-endpoints`
  and `/.well-known/webauthn` at the site root, mounted with `MapIdentityWellKnown`. The
  addresses of the frontend's password and passkey pages behind the first two are a
  declaration with no default: a deployment that registers none does not start, naming
  the declaration it left out, so both documents always answer. The third is the
  deployment's own related-origin allowlist.
- Where an account replaces its only address of a kind and holds no other channel at
  all, the address being displaced is asked to confirm the change, so a catalogue a
  deployment registers carries a template for `identifier-change-confirm` in every
  language it configures, or the deployment does not start.
- A person can sign in. A sign-in is begun against an identifier and answered with the
  entries the deployment enables, never with what the account holds, so an identifier
  nobody holds answers as one somebody holds does. A password, a passkey, a security
  key, a generated code, a recovery code and a link or a code the library sends are each
  judged by the service that owns them, and the answer carries the assurance the attempt
  has reached, whether it is phishing-resistant, and what it still needs. A second step
  is asked for whenever the account holds one, whatever the policy floor is, and a
  device the account has trusted is remembered for as long as the policy allows.
- A sign-in link completes the sign-in in the browser that asked for it. Opened in any
  other browser it changes nothing and shows the code to type back where the sign-in was
  begun, and a link the account abandons is spent at once.
- The account lists the browsers it knows, the ones it trusts for the second step and
  the ones the new-device check remembers, and forgets any of them: a trusted browser is
  asked for the second step again, a remembered one faces the check again.
- A sign-in in flight, a link or code sent for one, and a requirement a policy raised
  are kept in tables of the library's schema. Neither the handle a browser carries nor
  the link it was sent is held as it was issued: each is kept as its fingerprint, and
  the code beside it is held under the account's own key, where an erasure leaves it
  unreadable.
- An account whose policy allows it can recover a forgotten password from a link sent to
  the address or number it holds, and an address no account holds is answered the same
  way as one that does. Completing the recovery sets the password, stands a
  self-suspended account back up and ends every session the account held, and it clears
  no second step: the account still passes one at the next sign-in.
- An account that cannot be recovered by itself is re-enrolled by approvers, who
  must each write a reason, pass step-up, and confirm the person on a channel the
  account already holds; the number required is configurable, nobody can approve their
  own recovery, and an approver recovering many accounts, or many approvals of one
  account, is surfaced to the operator. The link the last approval sends is the only one
  that opens an enrolment session, and where the mailbox is the thing that was lost,
  that session may replace the address it is held on.
- The holder of a lost credential can report it, which refuses it from that instant
  without ending anything else the account can do, and invalidates it only after a
  window in which every notice sent carries a link that cancels the report. A window
  whose notices reached nobody holds the invalidation rather than completing it, and an
  invalidation that takes the last second step takes the recovery codes with it. An
  invalidation that leaves the account on a password alone that does not meet the
  single-factor floor requires that password to be changed: the next sign-in completes
  and says so, and the person is asked for a new password rather than locked out.
- A recovery link, an approval standing behind a re-enrolment and a running loss report
  are kept in tables of the library's schema, and the passwords table carries the mark
  that the next sign-in has to set a new one. The link is held as its fingerprint; the
  channel an approver confirmed on and the token the loss notices carry are each held
  under the account's own key, where an erasure leaves them unreadable.
- The recovery endpoints answer: asking for a link, completing one, reporting a
  credential lost and cancelling that report, approving a re-enrolment, and opening the
  enrolment session an approved link stands for. That session is bound to the browser
  that opened the link exactly as a registration is, so nothing else reaches what it may
  do.
- A person can manage their own credentials: setting or changing a password, enrolling a
  passkey or a security key against a challenge the server issued, upgrading a security
  key to one the authenticator keeps, enrolling a generator and confirming it with a
  code, taking a fresh set of recovery codes, and removing a credential. A second step
  is refused on an account that holds no password, a second step beside a password
  brings a set of recovery codes with it, an enrolment that leaves the account on one
  credential says whether a second is asked for or required, and a removal that would
  lower what the account reaches runs the notified window instead of taking effect at
  once. Each of these reaches every recorded channel, and each is gated at the lower of
  what the action asks for and what the account can reach. The enrolment session an
  approved link opens reaches the same operations without a session, and ends when the
  enrolment completes.
- A key ceremony in flight is kept in a table of the library's schema, one row per
  account. The row holds the challenge the server issued, what it is upgrading where it
  upgrades anything, and when it stops answering.
- The credential endpoints answer: setting a password, opening and completing a key
  ceremony, upgrading a security key, enrolling and confirming a generator, taking a set
  of recovery codes, and removing a credential. Each answers to the session the browser
  holds or to the enrolment session an approved link opened, and to nothing else; which
  of the two it is, is what the request's own session resolution established and never
  what the request says.
- A customer whose mailbox is gone can move their account to a new address from the
  enrolment session an approver opened for them: the new address confirms alone, the
  displaced one is not asked, and the approver's confirmation on a channel the account
  already holds is what stands in its place; the verification staged for it records no
  browser. Everywhere else an address is displaced only by a session that has stepped
  up, and the old address is asked where the account has no other channel at all.
