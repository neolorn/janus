# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/),
and the project follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html)
against the public contract of LIB-API-001.

## [Unreleased]

### Added

- An enrolment session reaches the routes `POST /enrol/begin` lists and no other
  credential route: `DELETE /account/credentials/{id}` and
  `POST /account/credentials/{id}/upgrade` refuse it 403 `authz.denied`, as do
  `ICredentials.RemoveAsync`, `UpgradeKeyAsync`, `GenerateRecoveryCodesAsync`,
  `LinkableAsync` and `UnlinkAsync`. `POST /account/recoverycodes/exported` now
  admits it, through the new overload
  `ICredentials.MarkRecoveryCodesExportedAsync(EnrolmentSessionId, CancellationToken)`,
  and asks the gate for the restriction there too. An enrolment session that has
  ended is answered 401 `auth.session.expired` with no details wherever it is
  presented, where the credential and identifier routes answered 422
  `auth.enrolment.tokeninvalid`; that code is now the link token's at
  `POST /enrol/begin` alone.
- `POST /account/recoverycodes/exported` and
  `ICredentials.MarkRecoveryCodesExportedAsync` refuse a restricted account 403
  `authz.restricted`, as every other change to its credentials is refused; no step-up
  is asked. A report made again leaves the first `exportedAt` standing.
- A recovery-code set carries `viewedAt` from the moment its codes are returned:
  `POST /account/recoverycodes` and the enrolment of a second step beside a password
  write it with the set, where it stayed unset until an export was reported.
- An alert and a loss report's notification count as carried only where the one
  attempt that follows their commit took them. One that attempt did not take counts
  as not carried whatever later becomes of its row: the alert falls to its second
  channel and the invalidation waits, also where the row was removed uncarried or a
  later pass carried it.
- A notice that a restriction refuses no longer spends its address's
  `abuse.nonexistent.window`: the window is marked only where the notice's send is
  admitted, for the notice to the holder of an address or number someone tried to
  register or add as for the answer to an address no account holds, so the next ask
  inside the window whose send is admitted tells the address.
- `IConsents.GrantAsync` that finds the consent recorded meanwhile, and so writes
  nothing, rolls its transaction back where it opened it and commits its level where it
  joined a unit of work the caller opened, so a caller's own unit of work is never
  marked by it.
- `IUnitOfWork.BeginAsync` answers `Result<bool>`: whether the level it opened is the
  outermost, or one that joined a unit of work another operation opened.
- The `expiry-sweep` job removes an identifier's add, and a replace whose swap has not
  applied, once every verification-code record it holds is spent or past
  `code.verification.lifetime`: the new address's code and, where the old address must
  confirm, that confirmation. A swept add is no longer listed on the account and no
  longer counts toward its kind's maximum, a swept replace leaves the identifier as it
  stood and no longer refuses a new replace with `identity.change.pending`, and the
  person asks again for a new code.
- An identifier added to an account is held on its pending verification until it
  verifies: no identifier is written before then, `GET /account` lists the add as an
  unverified identifier under the identifier the verified one keeps, and a kind's
  pending adds count toward `identifiers.email.max` and `identifiers.phone.max`. An add
  or a replace of a value another account holds, or an undo reserves, is staged as one
  of a fresh value is, with a record no code answers (422 `auth.code.invalid` up to
  `code.verification.attempts`, then `auth.code.expired`), where nothing was staged
  before; a restriction that refuses its ask answers 429 `auth.restriction.exceeded`
  and stages nothing. The right code, or a press of the link, for a value that has come
  to be held or reserved since it was staged writes nothing and is answered 422
  `auth.code.expired`, at an add and at the swap of a replace. `DELETE
  /account/identifiers/{id}` naming a pending add ends it as the abandon does, with no
  undo, notice, reservation, ended session or `IdentifierRemoved`. An undo leaves the
  account's own pending add of the restored value as it stands. An add or a replace of
  a value the account holds already writes nothing.
- The old address's confirmation of a replace lives `code.verification.lifetime` from
  its send: a press after it changes nothing and is answered 422 `auth.code.expired`,
  where it applied the change for as long as the replace was pending. A code or a
  confirmation outstanding on an add or a replace when this version is deployed no
  longer answers: the add is asked again, and the replace is abandoned and made again.
- A removal of an identifier, and the swap of a replace, replaces a removal record of
  the same kind and value whose undo window has run out and that the `expiry-sweep` job
  has not yet taken, where the second removal of such a value was a fault until the
  sweep ran.
- `POST /account/identifiers/{id}/undo` counts the account's verified identifiers of
  the kind alone: it answers 409 `identity.identifier.maximum` only where they fill
  `identifiers.<kind>.max`, and a pending add or an unverified identifier never refuses
  it, where an undo into a full kind was a fault. The right code or a press of the
  link of a pending add, at `POST /account/identifiers/{id}/verify`, answers 409
  `identity.identifier.maximum` and writes nothing where the account's verified
  identifiers already fill the kind; the add stays listed until it is swept or
  abandoned.
- `POST /account/identifiers/{id}/verify` counts and throttles its codes and presses
  as `POST /register/verify/{id}` does. A refused code is counted against the request's
  source and the identifier, an expired or capped one included, and a further code is
  answered 429 `auth.throttled` with `retryAt` while that delay stands. Every press is
  first held to the delay of its source. A pressed token that opens nothing (swept,
  abandoned, settled or never issued) answers 422 `auth.code.expired`, where it
  answered `auth.code.invalid`, and is counted against the source alone; one merely
  opened counts nothing.
- The expiry sweep ends pending identifier verifications in a transaction: it locks
  its candidates, passing over any row another transaction holds, and judges each
  again before it deletes, so a resend of an add's code, which holds the pending
  verification's row while it writes, keeps its record.
- `ICredentials.MarkRecoveryCodesExportedAsync` records that the person copied,
  downloaded or printed the recovery-code set the account holds, and
  `POST /account/recoverycodes/exported` maps it. An account holding no set is refused
  `auth.factor.notenrolled`. The instant is read as `exportedAt` in `GET /account`.
- `AppPasswordId` is a mail app password's identifier in the mail server's own form,
  the JMAP `Id` of RFC 8620 section 1.2: 1 to 255 letters, digits, `-` and `_`.
  `IAppPasswords.RevokeAsync` and `IMailServer.RevokeAppPasswordAsync` take it, and
  `AppPassword.Id` and `IssuedAppPassword.Id` carry it, where each was text; a host's
  own `IMailServer` answers its identifiers as this type. `DELETE
  /account/mail/apppasswords/{id}` binds it from the path, so an `{id}` outside the form
  is refused 400 `api.request.malformed` naming `id` and the mail server is not asked.
  The shipped adapter reads an app password the server names outside the form as an
  answer that does not read.
- `RestrictionName` names a sending restriction under the same rule as a document's
  name, read by `Parse`, `TryParse` and `IParsable<T>`. `IRestrictionSet.ReadAsync`,
  `DeleteAsync` and `GrantAsync` take it, where each took text, and the
  `/admin/restrictions/{name}` routes bind it from the path, so a name outside the rule
  is refused 400 `api.request.malformed` naming `name` before the body is read, where a
  body that did not read was named first.
- `DocumentName` names a legal document: 1 to 64 lower-case letters and digits
  separated by single `.`, `-` or `_`, read by `Parse`, `TryParse` and `IParsable<T>`.
  `ILegalDocuments.ReadAsync` and `TranslateAsync` take it and `DocumentPublication`
  carries it, where each took text, so a caller in process cannot read or publish under
  a name the routes refuse. `/privacy/documents/{document}`,
  `/admin/documents/{document}/versions` and its translation route bind it from the
  path: a name outside the rule is refused 400 `api.request.malformed` naming
  `document` before the body is read, whatever the body holds. An invitation whose
  `documents` names one outside the rule is refused 400 `api.request.malformed` naming
  `documents`, where it answered 422 `api.request.invalid`.
- Whether an email address or a phone number is held by an account or reserved for an
  undo, and whether a username is taken or held, is decided under a lock on the value,
  held to the end of the transaction that decides it. A registration's terms step, an
  identifier's add, verification, replace, removal and undo, a corporate address taken
  on at an acknowledgement, the bootstrap, a username's choice and an erasure's hold of
  a username take it before they judge or write, so two of them on one value run one
  after the other and the second judges what the first committed, where it could meet
  the unique constraint and be answered `system.fault`. The lock is taken once for
  each fingerprint key version the process holds, so processes on either side of a
  fingerprint key rotation wait on each other.
- A value reserved for an undo to an account is free to that account: it adds the
  value again, or replaces back to it, as it would a fresh one. Verifying the add,
  applying the replace, or taking the value on as a corporate address ends the
  reservation, and the undo link of the removal is then answered 422
  `identity.change.windowelapsed`. An undo pressed while the account's own add of the
  removed value still waits for its code restores the value and ends that add.
- An acknowledgement that would take on a corporate address another account holds, or
  one reserved for an undo to another account, is refused
  `identity.invitation.identifiermismatch` under the address's lock and attaches
  nothing.
- A lookup of an identifier, a reservation or a username hold by its value reads every
  fingerprint key version held in one statement.
- A `phoneCode` code asked for at `POST /auth/step-up`, where the number's signal answers
  `risk`, is no longer answered 202: nothing is issued, sent or counted, and the ask is
  answered 403 `auth.stepup.required` with `details` computed without the entry against
  the strictest of the gates of the policy in force, field by field, the step-up naming
  no action; `outcome` is `report-loss` or `enrol` where no combination is left.
- A sign-in code presented right whose sign-in a domain lock then refuses is spent in the
  one transaction that refuses it: the refusal `identity.identifier.domainnotallowed`
  counts no failure and records no failed authentication, where it used to be counted
  and recorded in a second transaction. A right code sent to an address the account has
  given up since commits its spend with the refusal's record and counts.
- A fault of the library's own while a factor, a sign-in code or a new-device code is
  being judged (a setting that does not read, the database failing) is no longer counted
  against the delay or recorded as a failed authentication, at a sign-in and at a
  step-up alike: the request answers `system.fault` and the person is not held for it.
- A `phoneCode` code asked for at `POST /auth/step-up`, where the number's signal answers
  `risk`, is answered as `POST /auth/factor` answers it: 200 `factorRequired` with
  `required` naming the factors of the combinations left without the entry, judged
  against the strictest of the policy's gates; 200 with `required` empty where the
  session already meets that gate, which used to be 202; and 403 `auth.stepup.required`
  only where no combination is left, with `outcome` `report-loss`, `enrol` or `pending`.
- Every event raised with the access context of a person who acted now carries
  `Effective` beside `Actor`, each as the context gives it: `AccountSuspended`,
  `AccountReactivated`, `AccountDeletionRequested` and `AccountDeletionCancelled` raised
  from a session or by an administrator, `CredentialEnrolled`,
  `SendingRestrictionChanged`, `SendingRestrictionGranted`, `TakedownReversed` and the
  `AlertRaised` of a destination change. `AccountReactivated` and
  `AccountDeletionCancelled` raised from the link a notice carried, with no context,
  carry neither, where they carried the subject as `Actor`.
- The drift check of materialised derivations records each grant it writes as
  `authz.grant.materialised` and each grant it takes back as `authz.grant.retracted`
  (`AuditActions.GrantMaterialised`, `AuditActions.GrantRetracted`), in the transaction
  that corrects the drift: a security record naming the principal
  `derivation-driftcheck`, the reason `AUTHZ-DERIVE-005`, the grant and its role.
- A privacy request whose receipt a sending restriction refuses is queued all the same:
  the request stands, its clock runs, and `receiptSentAt` is answered null by
  `POST /me/privacy-requests`, `POST /admin/privacy-requests` and the queue, where it
  was answered with the creation instant whether the receipt was admitted or not.
  `PrivacyRequest.ReceiptSentAt` and `PrivacyRequestReceipt.ReceiptSentAt` are now
  nullable, and a migration adds `privacy_requests.receipt_sent_at`, set to the
  creation instant for every request already queued.
- An invitation that is revoked, acknowledged, found expired by the sweep or attached to
  a subject who is erased holds, in place of its wrapped key, the 32 zero bytes of an
  erased key and no longer an absent one, so one erased value stands wherever a wrapped
  key is held; what it bound is cleared as before. `invitations.wrapped_key` is now
  required, `ck_invitations_key` holds the bound identifiers absent exactly where the
  key is the erased one, and a migration gives the erased key to every invitation
  already forgotten.
- The organizational security measures of the records of processing are spelled
  `organizational` wherever the library names them: the answer of `GET /admin/ropa`
  carries `organizationalSecurityMeasures`, as the request of
  `PUT /admin/compliance/assessments` already did; the public members are
  `ComplianceRecord.OrganizationalSecurityMeasures` and
  `ProcessingRegister.OrganizationalSecurityMeasures`; and a migration renames the
  column to `compliance_records.organizational_measures`, keeping the statement it
  holds.
- An outbox row is claimed whole before any subscriber is called, the erasure ledger's
  line among them, by one conditional update that succeeds only where the row is due
  and unclaimed or its claim has timed out, so one pass of the `outbox` job carries a
  row at a time whatever the number of processes. The pass renews its claim before each
  subscriber and stops where another pass has taken the row over; each confirmation is
  written as it happens and the row's attempts, schedule and status once, each only
  under the claim, and one pass counts one attempt. A completed erasure waiting for its
  ledger line is claimed the same way, so two passes do not append it twice. A
  subscriber still running when `outbox.claim.timeout` has passed is abandoned as one
  that did not confirm. The `outbox` table gains `claimed_until` (migration
  `ClaimAnOutboxRowBeforeItIsDelivered`).
- An emitted event's row is claimed whole before any consumer is called, by one
  conditional update that succeeds only where the row is due and unclaimed or its claim
  has timed out, so one pass of the `events` job carries a row at a time whatever the
  number of processes. The pass renews its claim before each consumer and stops where
  another pass has taken the row over; each consumer's take is written as it happens
  and the row's outcome once, each only under the claim. A consumer still running when
  `outbox.claim.timeout` has passed is abandoned as one that did not take the event.
  The `events` table gains `claimed_until` (migration `ClaimAnEventBeforeItIsOffered`).
- A message's row is claimed only where its next attempt is due, so a row one attempt
  released and rescheduled is not carried early by another. A new row whose immediate
  attempt follows the commit is written due `outbox.retry.initial` after its admission,
  with no jitter, and that attempt claims it whatever its due instant, so the `sends`
  job takes a new row only once its immediate attempt has had its chance. A message
  answered before any transport is called (a sign-in link, an email code, a recovery
  ask) has no such attempt and is due to the job at once.
- A message carried again that the restrictions or the gateway floor refuse fails for
  good: its row is removed without being carried, its count and the credit it spent are
  given back, and no alert is raised. It no longer waits as a failed attempt does.
- An ask of a sign-in link, of a recovery link or of a notice to an address no account
  holds, where the gateway floor refuses its text message, is answered as the ask would
  have been and nothing is sent. A verification code the floor refuses is refused
  `integration.sms.balancefloor`, alike whoever holds the number.
- The gateway floor is judged on the latest balance the `sms-balance` poll recorded,
  however old, and a send never asks the gateway for its balance. Until a first balance
  is recorded the floor refuses nothing. A poll the gateway does not answer records
  nothing and fails, so its lapse raises `background-job-failed`.
- A domain of an organization's lock, and the domain of an address judged against it,
  takes its ASCII form from the library's own UTS #46 processing and no longer from the
  machine's ICU, so one domain is listed and compared in one form on every machine. A
  domain with a hyphen in both its third and fourth places, a label under `xn--` that
  does not decode to a valid label, or a label that breaks the Bidi Rule is now refused
  as a domain that does not read; `xn--bcher-KVA.example` reads as
  `xn--bcher-kva.example`; a domain written with the full stop of another script reads
  with its labels separated. The two labels and the 236 octets are judged on the
  converted name.
- `CanonicalForm.TryDomainToAscii` gives a domain its ASCII form by UTS #46 processing
  at Unicode 17.0.0 from tables the package carries: nontransitional, with
  UseSTD3ASCIIRules, CheckHyphens, CheckBidi, CheckJoiners and VerifyDnsLength set and
  invalid Punycode refused, in lower case. The form does not depend on the ICU of the
  machine.
- The Public Suffix List's rules, and the hosts of `webauthn.origins` and
  `webauthn.relatedorigins` and the identifier of `webauthn.rpid` judged against them,
  are compared in that ASCII form and no longer in the machine's, so a deployment's
  registrable domain is the same on every machine. A rule of the list the conversion
  refuses is set aside when the list is read. An origin whose host, or an identifier,
  the conversion refuses (an underscore, a hyphen first or last or in the third and
  fourth places, an empty label) stops the start with `model.startup.rpid`, where the
  machine's mapping admitted some of them. The labels a related-origins allowlist is
  counted by are compared in their ASCII form, so one name written in Unicode and in
  its ASCII form counts once.
- A refused factor's record, the delay's counts and a trusted device's failure are
  committed in one transaction, and none stands where one cannot be written. A wrong
  sign-in code or new-device code commits its count on the code, or the code's removal
  at the cap, in that same transaction; a code presented past its lifetime is no longer
  removed by the presentation and is left to the sweep. A passkey or security key whose
  signature counter did not advance commits `auth.credential.countermismatch` with the
  failed authentication's record and the failure's counts, and an assertion refused
  after its counter was read no longer advances the stored counter.
- A break-glass presentation runs in one transaction. A refused code, the code of the
  issue last used presented again included, commits the attempt's count, the source's
  failure and the failed authentication together, and a refusal by the global limit
  commits the count with its `auth-failures-sustained` raise. A consumed code is now
  recorded as a failed authentication and counted against its source.
- The phone signal callback is asked before an operation's transaction begins and never
  inside one. What it answered is recorded in the transaction that undertakes the
  restricted factor, and a restricted factor undertaken for a number nothing was asked
  about is a fault.
- A fault of the library's own in a send's immediate attempt after the commit (a
  setting that does not read, the database failing at the claim or at the outcome) is
  logged and left to the publisher's next pass. The operation answers what it
  committed instead of `system.fault`.
- A suspension, a revocation of an invitation, and a request or a cancellation of an
  organization's deletion that another caller made first answer as before and roll
  their transaction back.
- A turn of the background worker that finds no lapse to claim rolls its transaction
  back.
- Adding a member a group already holds and taking out one it does not hold answer as
  before and roll their transaction back.
- A withdrawal of a consent or of an objection made meanwhile, an objection that meets
  one recorded meanwhile, the erasure of an organization whose window was cancelled
  meanwhile, and a pass, a sweep or a completion of a key rotation that finds nothing
  left to do each answer as before and roll their transaction back.
- A trust or a remembered browser found revoked under its lock, and the end of a
  suspension window for a loss report cancelled meanwhile, answer as before and roll
  their transaction back, having written nothing.
- A rotation of a registered client's secret that another process made first, a change
  of the signing keys with nothing due, and a longer lifetime stored against a key no
  longer current each answer as before and commit nothing: an operation that succeeds
  having written nothing ends its transaction by rolling it back.
- A delivery report that fails for any cause but its rejection (a setting that does not
  read, a raise that cannot be written) leaves nothing of itself behind and no
  transaction open. A rejected report, like every rejected callback, keeps its
  admission count, its rejection's count and the `callback-verification-failed` raise.
- A route or query value that does not read as its type is refused 400
  `api.request.malformed` with `details.member` naming it, the first in the order the
  endpoint declares where more than one does not read. An identifier in a path that is
  not a UUID was answered 404 before and is now this refusal, and a restriction name
  outside the rule of a name is refused the same way on every `/admin/restrictions`
  route that takes one. `RoleName`, `ResourceType`, `ResourceId` and
  `ConfigurationKey` implement `IParsable<T>`.
- A second step (a code generator, a security key under two-step) or a recovery-code
  set asked for on an account that holds no password is refused 409
  `auth.factor.passwordrequired`, where it answered 422 `auth.factor.notpermitted`.
  The error catalogue also gains `auth.factor.notenrolled` (409), for an account that
  holds no enrolment of the kind an operation acts on.
- The gate judges a processing restriction with the account's row held wherever it is
  asked for a modifying action inside an open unit of work, a host's own included: the
  row is locked `FOR SHARE` to the end of that transaction and the state is read under
  the lock, so a restriction commits before the action, which is then refused
  `authz.restricted`, or after it. A reading action holds nothing.
- Granting and revoking a grant, defining and removing a role, and creating, removing
  and changing the members of a group ask the gate again inside their unit of work
  before the first write. A restriction of the acting account committed after the first
  ask refuses the change `authz.restricted` and leaves nothing written.
- The administrative changes of accounts, organizations, their policies, domains,
  invitations and memberships, sessions, the restriction set, maintenance records, the
  break-glass credential and a recovery approval, and an account's own changes of its
  profile, preferences, photo, credentials, deactivation and invitation
  acknowledgement, ask the gate again inside their unit of work before the first
  write, with the acting account's row locked first. A restriction committed after the
  first ask refuses the change `authz.restricted` and leaves nothing written.
- Publishing and translating a legal document, declaring the processing records,
  entering, fulfilling and refusing a privacy request, executing and reversing a
  takedown ask the gate again inside their unit of work before the first write. A
  restriction of the acting account committed after the first ask refuses the change
  `authz.restricted` and leaves nothing written.
- Changing a configuration key or a declared category's retention through the
  administration interface asks the gate again inside its unit of work before the first
  write, and the setting's one writer joins that transaction. A restriction of the
  acting account committed after the first ask refuses the change `authz.restricted`
  and leaves nothing written.
- An account's own changes of its identifiers (adding one, verifying one by its code,
  making one primary, setting the backup, removing one and replacing one) ask the gate
  again inside their unit of work before the first write, with the acting account's row
  locked first. A restriction committed after the first ask refuses the change
  `authz.restricted` and leaves nothing written.
- Creating a mail app password asks the gate again after the mail server's call, inside
  the unit of work that records the creation. A restriction of the account committed
  meanwhile refuses the creation `authz.restricted`: the password the server created is
  revoked there before the answer, its secret is never returned, and nothing notifies or
  audits it. Where the server does not take the revocation the refusal is answered all
  the same, the failure is logged, and the password stays listed for its holder to
  revoke.
- An approved recovery writes the approval, its audit record and the enrolment link's
  send in one unit of work. A link a sending restriction refuses is answered 429
  `auth.restriction.exceeded` with `retryAt`, and one the gateway floor refuses 422
  `integration.sms.balancefloor`; either leaves no approval, no record of it and no
  send, and so does a restriction of the approver's account committed after the first
  ask of the gate, answered `authz.restricted`.
- The manual completion of an erasure asks the gate again inside its unit of work
  before the first write. A restriction of the operator's account committed after the
  first ask refuses the completion `authz.restricted` and closes nothing. A ledger line
  the completion appended before its unit of work stands, and a replay reads a line
  appended twice as one erasure.
- A change of an alert destination list asks the gate again inside its unit of work
  before the first write. A restriction of the acting account committed after the first
  ask refuses the change `authz.restricted`: nothing is written and no
  `alert-destination-changed` is raised, and the notice already given to the
  destinations it would have replaced stands as the notice of a change requested and
  not made.
- The shipped mail-server adapter lists an account whose `emailAddress` does not read as
  an email address, or that holds none, with no address, where it failed the listing
  for an account holding none. Reconciliation reads such a listing whole: the account
  is counted where it carries no held mailbox's identifier and is a difference where it
  carries one.
- The two log entries of a concealed refusal carry the audit record they name as an
  `AuditRecordId`, in the text it was always written in.
- Every count and delay of a registration uses the source of the request in hand, never
  the address its begin arrived on: `IRegistration.StageAsync`, `AddAsync`,
  `ChangeAsync`, `VerifyAsync` and `LandAsync` take that source, and the address given
  to `BeginAsync` is the whole address the session holds. A pressed registration link
  token that opens nothing is held to its source's delay, counted against that source
  alone and answered `auth.code.expired` (`auth.throttled` with `retryAt` while the
  delay stands), also from a browser holding no registration session; one merely
  opened counts nothing.
- A source, for every count the library keeps per source, is the address the connection
  arrived on with an IPv4-mapped IPv6 address read as its IPv4 address and any other
  IPv6 address counted by its /64, so one host no longer holds a budget for each
  address of its subnet; an IPv6 address whose first three bits are 000 is counted
  whole. The flood limit also counts each IPv6 source's enclosing /48 under the new key
  `abuse.source.sitelimit` (3000 a minute, sliding, per instance), and a request
  refused while its source or /48 is held writes no log line of its own. A session
  still records the whole address, and the session a registration opens records the
  whole address of the request that completes its terms step.
- A text code asked for at a sign-in, after a first factor, whose number's signal
  answers `risk` is no longer answered 202: nothing is issued, sent or counted, the
  consideration is recorded, and `POST /auth/factor` answers with what the challenge
  then offers, 200 `factorRequired` naming the factors left or 422
  `auth.factor.rejected` where none is. An ask before a first factor is still 202
  and sends nothing.
- A session records the instant it was last downgraded (`sessions.downgraded_at`,
  migration `RecordWhenASessionWasDowngraded`). Acknowledging an invitation downgrades
  every session the account holds, in the transaction that attaches the membership:
  what a session attained up to then passes no step-up gate until a factor the policy
  in force permits is presented, which lifts the downgrade, and the attained values
  stay as they were reached. A capability whose gate the session would meet but for
  its downgrade carries `requires` `reauthenticate`; any other unmet gate carries
  `stepup`.
- At a step-up, a factor the policy in force does not permit is refused with
  `auth.factor.notpermitted` before it is verified, whether it is right or wrong, and
  counted as a refused step-up factor. A sign-in goes on refusing it only after the
  factor has succeeded.
- A host's assurance report meets a step-up gate only where it reads: a report whose
  instant is after now, or whose level or reachable assurance is not an assurance
  level, meets no gate and is refused with `auth.stepup.required` as a report the
  provider fails to give is.
- An account that holds no verified email cannot acknowledge an invitation into an
  organization whose domain lock is on: it is refused with
  `identity.identifier.domainnotallowed` and nothing is written, as an account whose
  verified emails are all outside the list is.
- `CredentialSuspended` carries the effective identity of the context that reported the
  loss or asked for the removal as `Effective`, beside its `Actor`, as
  `CredentialRestored` from a session does. Both are carried as the context gives them.
- `IUnitOfWork.RollbackAsync` ends an operation with nothing of it saved: the
  transaction, every tracked change and every registration to run after the commit are
  discarded. It takes no cancellation token and answers no result. An operation that
  joined another's unit of work and rolls back marks the whole, and the outer commit
  then commits nothing and throws. A commit that fails leaves the unit of work rolled
  back. The analyser JAN0004 now reports an asynchronous member that implements one of
  the library's own interfaces without a cancellation token.
- `Janus.Conformance`, the suite a host runs against its own deployment, each call
  answering a report whose findings are codes with structured data.
  `ConformanceSuite.Policies` names each entity of the host's context that is not a
  declared resource type, the rows of a declared relationship or a contract table, under
  `authz.policy.unregistered`. `ConformanceSuite.Declaration` judges a declaration by
  the checks startup runs and reports a refusal under its own code.
  `ConformanceSuite.TruthTableAsync` writes each case of the host's table into the
  deployment and reports, under `authz.truthtable.disagreement`, each case the single
  check or the list filter decides otherwise than the table states. A derived case is
  written once for each derivation declared at the level it uses, and its finding names
  the derivation's relationship under `details.derivation`. It writes into the
  database, so it runs against a deployment kept for it.
  `ConformanceSuite.ProviderAsync` asks the provider each form AUTH-OIDC-006 retires and
  reports, under `auth.oidc.nonconformant`, each one it admits or its discovery document
  lists, a push naming a destination other than the client's registered one included,
  and a refusal that still hands back a `request_uri`. The requests are made by the library's own half of the sign-on, as the client
  the host declares for the application, so the suite takes the deployment and who asks
  and nothing else: no client is registered for it and it holds no secret.
  `IProviderProbes` in `Janus.Core` is the operation it asks through, called in process
  only.
- `ConformanceSuite.TruthTableAsync` takes a `DeploymentFactory`, a delegate the host
  supplies that builds and starts its composition with the `IAssuranceProvider` it is
  given registered, or with none where it is given none, and runs seven step-up
  scenarios through it: `stepup-met`, `stepup-level-unmet`,
  `stepup-phishingresistance-unmet`, `stepup-age-unmet`, `stepup-instant-future`,
  `stepup-provider-failed` and `stepup-provider-absent`. The factory is called once for
  each step-up case, with a provider of the suite's own that gives the scenario's report
  or fails to give one, and with none for `stepup-provider-absent`; the composition it
  answers is disposed once the case is judged, and every other case runs on the
  container passed. A step-up case agrees where the check answers the gate's outcome
  and the filter answers the same, and its finding names the action's gate under
  `details.gate`. A `TruthTableCase` of a step-up scenario states `Allowed` true for
  `stepup-met` and false for the rest; one stating otherwise throws where it is
  constructed, and a table whose step-up case names a permission bound to no gate is
  refused before anything is written. The members of a `TruthTableCase` are set at
  construction alone.
- The `DeploymentFactory` of `ConformanceSuite.TruthTableAsync` is optional: a host
  whose table holds no step-up case passes none, and a table that holds one and is
  given none is refused with an `ArgumentException` naming the scenario before
  anything is written.
- `IResources` in `Janus.Core`: a host registers each record it creates, many at once
  for an import, and moves one, inside its own unit of work, and the ancestry the
  permission filter reads is written in the same transaction. A record is placed only in
  a container of the type its own is declared contained in and of the same organization.
  A type the model does not declare is refused as `api.request.malformed` naming
  `resourceType`; a record registered already or not registered to move (`resourceId`)
  and a container that cannot hold it (`containedIn`) are refused as
  `api.request.invalid`, and a refused batch writes nothing. A record of a sensitive
  type names a subject holding an account that is neither being deleted nor deleted, or
  it is refused as `api.request.invalid` naming `subject`.
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
  naming its issuer and keys, the deployment's client identifiers there, and the address
  the provider returns the browser to. Startup stops a deployment under
  `model.startup.declarationinvalid` where a provider is declared twice or is not a
  social provider (`details.field` `provider`), where its keys address or its discovery
  address is not HTTPS (`metadata`, `configuration`), where its return address is not
  HTTPS or does not end in `/callbacks/providers/{provider}/return` for the provider it
  is declared for (`return`), or where it has no client (`clientIds`);
  `details.declaration` names the provider as `socialProvider.<provider>`. What the
  application presents at the provider's token endpoint is read, as the deployment
  starts, through `ISecretSource.ReadProviderCredentialAsync` by the provider's name, as
  a `ProviderCredential`: a static secret the provider issued, or a signing credential
  (the issuer the provider knows the account by, the key identifier and the P-256
  private key it issued), from which a client secret signed with ES256 and good for five
  minutes is minted at each exchange and never stored, so a secret like Apple's never
  lapses and never needs a restart. A credential the source cannot answer, answers
  empty, or answers as a signing credential with a blank issuer or key identifier or a
  key that is not a P-256 private key stops the start under
  `model.startup.secretunavailable`, `details.key` `socialProvider.<provider>`.
- `POST /account/link/{provider}` answers `204` where the signed-in account may link the
  provider, and `DELETE /account/link/{provider}` unlinks it at the provider-unlink
  step-up. Unlinking the only way left to sign in to the account, here or through the
  credential endpoint, is refused with `409 identity.link.lastcredential`.
- A round trip to a social provider is kept in a table of the library's schema while the
  browser is away, one row per browser. The proof key is kept wrapped under the
  deployment's data key; the row goes when it is taken or when the session it belongs
  to ends.
- The annual operation on the envelope, in which the key-encryption key is rotated, is
  warned of: a daily job, `envelope-rotation`, raises `expiry-approaching` from
  `maintenance.expiry.warninglead` before a year has passed since the last
  `envelope-rotation` entry of the maintenance log, and goes on raising it until the
  next one is recorded. A log that records none has the operation due at once.
- The same daily job warns of the key-encryption key's cryptoperiod from the key's own
  record: from `maintenance.expiry.warninglead` before a year has passed since the
  latest completed `rotate-kek` rotation, or since bootstrap where there has been none,
  it raises `expiry-approaching` under `kek-cryptoperiod` with the version, when it was
  rotated in and when it is due, until a rotation completes. No maintenance log entry
  and no rotation of the fingerprint key ends it.
- `janus register-client` registers a client in the provider's registry, or changes a
  registered one, from the server: `--client`, `--name`, `--kind`, `--redirect` and
  `--scopes`, and no secret. The library draws a client's secret at its first
  registration and holds it wrapped under the deployment's data key; registering a
  client again changes it and leaves its secret. Each registration is recorded as
  `auth.oidc.clientregistered`, with nothing of the secret.
- A registered client's secret is replaced at `token.signing.rotation` by the first read
  that finds it due, while the deployment runs; of two instances that find it due
  together one replaces it and the other presents what the first wrote. The provider
  takes the secret it replaced for the access-token lifetime and five minutes, compared
  in fixed time. No person chooses, carries or rotates a client secret. The migration
  that moves the registry to held secrets refuses to run where a client is registered,
  since what a secret hashed to cannot become the secret.
- The audit trail's monthly partitions are kept by a daily job, `audit-partitions`,
  under the maintenance credential: the current month and the two after it are created
  where missing, and partitions past `retention.audit.security` or
  `retention.audit.routine` are dropped. The job refuses any other credential, and each
  run is recorded as `ops.auditpartitions.maintained`.
- The restore test runs by itself every `backup.restoretest.interval`, 90 days by
  default and at most. A deployment
  registers `IRestoreTestInstance`, which restores its latest backup into a throwaway
  instance and tears it down again; the library opens the restored database with the
  keys it runs on, decrypts the canary's field, finds the canary's account by its
  verified email, and times the whole against `backup.restoretest.objective`. Every run
  is recorded as `ops.restoretest.completed` with its outcome, the seconds it took and
  the objective. A run that restores nothing, cannot decrypt, cannot find the account,
  runs past the objective (it is abandoned there, and recorded as an overrun whichever
  step it was on) or whose instance may still stand raises `restore-test-failed`. Without an `IRestoreTestInstance`, every run raises it.
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
  deployment that registers no ledger completes its erasures without one. Once a ledger
  is registered, the outbox worker appends, once and oldest first, the line of every
  erasure completed before it was, a manual completion included, without changing the
  erasure's status, attempts or erasures row; a line the ledger refuses is tried again
  on the next pass.
- The `privacy.erasure.executed` audit record, whether the deletion sweep or
  `replay-erasures` writes it, carries `details.reason` by its written name
  (`erasure-request`, `minor-takedown`), as the ledger line and the erasures table do.
- Every audit record names the account an action was taken on as its `subject`, apart
  from both identities, and records whoever acted as its acting and its effective
  identity, a break-glass session included, and the nil subject for both under a system
  principal. The trail read by subject returns the records naming the subject as acting
  identity or as `subject`, with the principal and the reason of background work
  (`AuditEntry.Principal`, `AuditEntry.PrincipalReason`). A record written before names
  the account as its effective identity and no `subject`.
- An entry of the audit trail read by subject carries the data subject its record
  concerns (`AuditEntry.Subject`, `subject` on `GET /admin/audit`), null where the record
  concerns none.
- A refusal of background work is recorded as its other actions are: the nil subject
  under both identities, with the principal's name and stated reason. Its correlation
  identifier resolves to them (`ExplainedPrincipal.Name`, `ExplainedPrincipal.Reason`;
  `principal.name` and `principal.reason` on the explanation routes, present only for a
  system principal). A migration makes both identities required on every audit record
  and stops where the trail holds a record naming none. `denial-spike` counts a system
  principal's refusals by its name, so each principal is an actor of its own and the
  alert names it; refusals recording the nil subject and no principal count as one
  actor.
- The record of a gate refusal, the count of its actor's refusals and the `denial-spike`
  alert that count raises, with its `AlertRaised` event, are written in one transaction
  of their own, outside the caller's and committed at once, so a spike reached inside
  work that rolls back is still raised. The actor's refusals are held while they are
  counted, so two refusals at once are counted one after the other, and a spike that
  cannot be written fails the record.
- A host declares a relationship source for every relationship a declared derivation is
  over, materialised or not: `RelationshipSource.Of<TContext, TRow>(relationship, rows)`,
  registered once per relationship, naming the host's own context, which maps the
  contract tables and which the container gives in a scope, and answering the
  relationship's rows from it. A derivation whose relationship has no source stops
  startup with `model.startup.declarationmissing` naming the relationship. A source given
  twice, naming no declared relationship, answering another row type than the
  derivation's, or naming a context the container does not give in a scope or whose
  model does not map the contract tables stops it with
  `model.startup.declarationinvalid` (`details.declaration`
  `relationshipSource.<relationship>`, `details.field` `relationship`, `rows` or
  `context`).
- The job `derivation-driftcheck` evaluates every materialised derivation over its
  relationship source every `derivation.materialised.driftcheck`, in one statement in the
  host's context, brings the materialised grants that no longer match the host's rows
  back into step and raises `degradation` naming the derivation (`details.derivation`)
  in the same run. A grant it writes or takes back records the nil subject and the reason
  `AUTHZ-DERIVE-005`.
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
  the permission each count as one export; a capability page does not. The expiry
  sweep forgets every export admitted more than an hour ago. Turning
  `exfiltration.export.stepuprequired` off raises the High `stepup-policy-weakened`
  alert naming the key as the change is made, and a change whose alert cannot be
  raised is not made.
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
  answer to `compliance:manage`. Two licences under one identifier and an entry dated
  after now answer 422 `api.request.invalid` naming `licences` or `performedAt`.
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
  the expiry sweep; a failed one stays. Publication is not a default a host replaces: an
  `IEvents` registered before `AddJanus` is not used, and a host consumes an event only
  through `IEventConsumer<TEvent>`.
- `configure` changes protected keys from the server, the one way to change a key the
  management application refuses: pipe the key document to it as to `bootstrap` and name
  each key as `--<key> <value>`, with `--reason`. It takes the keys chapter 10 section
  4.8 protects and refuses every other key. Audit logging, token signature verification
  and step-up enforcement have no switch: the library performs them unconditionally.
  The facts a deployment declares about itself (the WebAuthn origins and algorithms, the
  hosting location and cross-border basis, the shipped transports' endpoints, the new
  `integration.mailserver.endpoint` and the default client) are on that one list; a
  mail server endpoint that is not an absolute `https` address stops startup with
  `integration.endpoint.insecure` naming the key. Each change is recorded under the
  `configure` principal with its reason and what the key was (its default where no
  value was written, nothing only for a key the deployment names and never named) and
  raises `protected-setting-changed`; the
  governing language also raises `governing-language-changed`, each with its
  `AlertRaised` event written in the change's transaction; a change whose alert cannot
  be raised is not made. A change that would leave
  the deployment unable to start is refused and nothing of it is written: the command
  runs the start's checks over the written values (the keys to name, the relying party,
  the endpoints, the signing algorithm and the default client) and refuses with the
  code the start would give.
- A change to the system policy or to an organization's policy that leaves any step-up
  gate asking less (a lower level, phishing resistance dropped, or a longer maximum age)
  raises the High `stepup-policy-weakened` alert as it is made, naming the policy key
  and the gates. An organization's alert is raised under that organization.
- `rotate-fingerprint-key` rotates the fingerprint key from the command line under the
  maintenance credential, as `rotate-kek` rotates the other: add the new version as
  current, keep the previous one, restart the application on it, and pipe the document
  to the command. It computes every stored fingerprint again from the value beside it,
  resumes where it stopped, and prints the new version's escrow copy. Once the copy is
  sealed, `rotate-fingerprint-key --sealed` retires the previous versions and prints
  `keepUntil` as `rotate-kek` does; it refuses with `model.rotation.notready` while a
  username held after an erasure, an address an erased account gave up, or an abuse
  count (a throttle counter, a send or its counter, a registration start, a notice or a
  callback) is still standing under one of them. The expiry sweep removes, under every
  version, each such count once its own check no longer reads it, so a retirement waits
  on nothing that has stopped counting. A social sign-in link also holds the
  provider's subject encrypted under the account's key.
- Every value the library encrypts that belongs to no subject (an invitation's
  identifiers, a mailbox reserved for nobody, a registration session, a queued message,
  a sign-on proof, a signing key, a social provider's proof key) is under the
  deployment's data key, a row of the subject-key table under the max UUID (all 128 bits
  set), which no subject is issued, that the key-encryption key wraps like any subject
  key and that erasure refuses to touch; nothing else is wrapped directly under the
  key-encryption key. `SubjectId` cannot be made from the max UUID, and every column of
  the library's tables that can hold a subject identifier, the audit identities, a
  grant's holder and a group's member included, refuses it by a check constraint; only
  the subject-key table's key and the key rotation's point in it hold it. What is encrypted for such a row (an invitation's identifiers, an
  unheld mailbox's address, a queued message naming no subject, a registration
  session's staged values) is bound to that row, so it does not decrypt on another. The
  migrations run only on a database that holds no such value, since the database can
  neither re-wrap nor re-bind it; the move to the max UUID names the table where one
  stands.
- `rotate-kek` rotates the key-encryption key from the command line under the
  maintenance credential. Add the new version to the secrets manager as current, keep
  the previous one, restart the application on it, and pipe the document to the command:
  it re-wraps every row of the subject-key table in batches of 500, the deployment's
  data key among them, writing a key back only where it still holds what was read,
  and touches no other table; it resumes where it stopped when run again, and prints
  the new version's escrow copy. Once the copy is sealed,
  `rotate-kek --sealed` retires the previous versions: they leave the application's key
  document at once, and the envelope and the secrets manager after `keepUntil`, the date
  it prints, which is the rotation's completion and `backup.retention` (the default, or a
  longer one named with `--retention`). It refuses with `model.rotation.notready` while no
  rotation awaits a seal or anything is still wrapped under them (`details.pending`
  counts it). Each step
  is audited under the `rotate-kek` principal. Refresh tokens issued before the rotation
  stop reading once the previous version is removed.
- A command-line application stands a fresh deployment up with `bootstrap`. It takes the
  organization's name, the first administrator's email and phone, optionally the
  corporate address whose mailbox is queued for them, and each required deployment value
  as `--<key> <value>`; any other key is refused. The database connection, the
  key-encryption keys and the fingerprint keys are read once from a JSON document piped
  to standard input, never from a terminal, an argument or the environment; a terminal,
  a document that does not read, or one without a usable key is refused with
  `model.startup.secretunavailable`, `details.key` naming `input` or the key. It writes
  the named values, the three administrative roles, the administrative organization and
  its policy, the administrator, the reserved `emergency` account holding the role and
  no way in, and the restore test's canary, raises the alert that no emergency
  credential exists, and prints the administrator's enrolment address,
  `<first webauthn.origins entry>/link#enrolment.<token>`. The named origins settle the
  relying party as the start settles it, before the database is reached, so origins no
  identifier sits over are refused with `model.startup.rpid` or
  `model.startup.labellimit`. It refuses to run where a system administrator exists or
  ever existed. A refusal is one JSON line on
  standard error, with exit code 1; a command the application does not carry is refused
  the same way, `api.request.malformed` naming it. What it defines and sets is audited under its own
  principal, for which `SystemOperation` carries `Bootstrap`; each value it sets is
  recorded with what the key was, the written form of its default where no row stood and
  nothing for a required key. Its grants name the nil
  subject as their granter, and each membership it attaches emits `MembershipChanged`
  (`began`) with the rows, as the missing emergency credential emits `AlertRaised`.
- A runtime change is decided on the value in force under its row's lock: the change
  of a key, of an organization's policy (with the system policy's row) and of its
  domain lock each take the row before reading the value and classifying, so a
  concurrent change waits and cannot turn a tightening into a loosening.
- `outbox.poll.interval`, which also paces the carrying of raised alerts, has a ceiling
  of one minute and is refused above it with `config.value.aboveceiling`, at the change
  and, for a stored value, as the deployment starts: startup reads every key, so any
  stored value its key does not admit stops it there. The balance
  poll fails where no SMS transport is registered, so its lapse raises
  `background-job-failed`.
- The library runs its own scheduled work. A worker `AddJanus` registers sweeps expired
  sessions, codes, links and tokens, ends the windows of account deletion, organization
  erasure, loss reports and privacy-request deadlines, re-verifies locked domains,
  publishes the outbox, provisions mailboxes, carries raised alerts, reconciles the mail
  server daily and reads the gateway balance. Two passes that carry the same condition
  at once deliver it once. Each job runs as a named principal of its
  own, once across the processes of a deployment, and a long run holds no other job's
  turn: a job has one run at a time in flight in a process, and the worker waits for the
  runs in flight when it stops. A job whose last success is older
  than twice its interval raises `background-job-failed`; the lapse of `alert-dispatch`
  itself is also delivered straight from the worker, so a stalled carrier still reports
  itself. `SystemOperation` carries
  `Delivery` and `Monitoring` for this work, and the runs are kept in a table of their
  own.
- Background work acts as a named system principal that states its reason, and is
  audited as one. Every job's pass runs only as a principal that may do the job's
  operation (sweep what has expired, deliver, reconcile, monitor or purge), and is
  refused to a person or to a principal named for other work. What the passes record
  carries the principal's name and reason where a person's action carries the acting
  account.
- The sealed break-glass credential. `POST /admin/break-glass/generate` generates it,
  for a stepped-up system administrator or from a break-glass session, and answers the
  code once, in nine check-charactered groups of four, with the absolute `/break-glass`
  address the envelope prints; a new code invalidates the one before it, and only an
  Argon2id hash of it is kept. `POST /auth/break-glass` takes the code and the reason
  the owner gives, free text that is required, on the machine profile, ignoring any
  cookie the browser holds, and opens an auth session for the
  reserved `emergency` account that passes every step-up gate for
  `breakglass.session.lifetime`; idle past its policy's inactivity window, it asks for
  a full sign-in, never the one-factor restore. A code opens one session; a group whose
  check character is wrong is refused before any hash is compared; at most five
  attempts an hour are taken from all sources together, besides the per-source delay,
  and the first attempt the limit refuses raises `auth-failures-sustained` for the
  reserved account. Generation and use are audited under `auth.breakglass.generated`
  and `auth.breakglass.used`, and raise `breakglass-generated`
  (`AlertCondition.BreakGlassGenerated`) and `breakglass-used`, each High and scoped to
  the issue, to the operator and to the owner whatever `alerting.owner.enabled` says.
  The session keeps the reason, as does a session another application opens from it,
  and every audit record either writes carries it in a column
  of its own, `auth.breakglass.used` included; the trail read returns it as
  `breakGlassReason` (`AuditEntry.BreakGlassReason`), and a record of background work
  never carries one.
  `GET /admin/break-glass` answers a system administrator, with no step-up, whether a
  code stands and when it was generated, `{ "standing", "issuedAt" }`, and nothing of
  the code. A host generates the credential in process through `IBreakGlass`, which
  also reads whether one stands and since when, and asks what the routes ask:
  `system:administer`, and a step-up to generate. The reserved account is never suspended, taken down,
  deleted, granted anything or added to a group, and is given no password, identifier,
  factor, provider link, recovery codes or mail credential; each is refused with
  `authz.denied`. Its `system-administrator` grant is never revoked, and the role it
  holds never loses a permission the library declares (a host permission may be
  added), each refused with `authz.denied`. The reserved account is marked on its row,
  and the credential and its attempts are kept in two tables of their own.
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
  the account with a security notice where no other usable credential, the password
  included, may begin a sign-in; a disabled relay address drops to unverified. A carried
  or repeated event is answered 202 on the Google route and 200 on the Apple route. A
  token that fails validation on the Google route is answered as RFC 8935 section 2.3
  fixes: 400 with `Content-Language: en` and a body of `err`, the code of the first
  failure in a fixed order, and `description`, which carries the same code: a token
  that cannot be read or carries no `jti`, `invalid_request`; a provider not declared,
  `invalid_issuer`; a key the published set does not hold or a signature it does not
  verify, `invalid_key`; another issuer, `invalid_issuer`; an audience naming no
  declared client, `invalid_audience`; a lifetime that has passed, `invalid_request`.
  On the Apple route each of these is refused as every rejected callback is, 422
  `integration.callback.rejected`. On either route a provider document that cannot be
  read refuses nothing: the delivery is answered 500 `system.fault`, nothing is
  claimed, recorded or counted, and the provider may deliver the event again. The
  client the documents are read with is `identity-providers`.
- `UseCallback` mounts one of the host's own providers' callbacks on the machine
  profile, at a path the host chooses and ahead of the browser profile. A signed
  callback (`ISignedCallback`) names its provider's keyed hash, where the signature and
  the signed bytes are, its secrets and its event identifier; the library verifies the
  signature over the raw bytes in fixed time against the current secret and, for 24
  hours after a rotation, the previous one, holds a five-minute window where the scheme
  carries an instant, and carries each event once: the claim is settled when the host's
  route answers with a 2xx and given back when it does not or throws, a delivery meeting
  a claim still unsettled is answered 409 `integration.callback.inprogress` (not a
  rejection), and one meeting a claim left unsettled for
  `integration.callback.claimtimeout` (five minutes by default, at least one) takes it
  over and is carried. An unsigned callback (`IUnsignedCallback`) reaches the route only
  with a reference issued for it and once the host has confirmed it with the provider.
  Both are held to `integration.callback.ratelimit` and to the provider's published
  ranges first. A refusal is answered `integration.callback.rejected`: 429 with
  `Retry-After` and `details.retryAt` where the rate limit refused it, 422 with no
  `Retry-After` otherwise, and every refusal but the rate limit's is recorded against
  its source and counted toward `alerting.callback.threshold`. Claimed events and issued
  references are kept, as hashes, in the `callback_events` and `callback_references`
  tables, a claim with when it was taken and when it settled.
- A machine route that refuses a request for carrying the session cookie answers as its
  protocol refuses a bad request, where it answered 403 `authz.denied`. A callback, the
  delivery report, the two provider-event routes and the host's own included, is held
  to `integration.callback.ratelimit` first and then refused 422
  `integration.callback.rejected` with no `Retry-After`, recorded against its source and
  counted toward `alerting.callback.threshold`; the Google route answers it so too, not
  in the RFC 8935 shape. `POST /oidc/par`, `POST /oidc/token` and `GET /oidc/userinfo`
  answer 400 with `error` `invalid_request` alone, before anything the request presents
  is read.
- `ICallbackReferences.IssueAsync` issues the correlation reference an unsigned callback
  carries: 128 random bits in base64url, of which only the hash is kept.
- `GET /callbacks/sms/dlr` takes the SMS gateway's delivery report on the machine
  profile. `ISmsTransport.ReadReport` reads the report from the parameters the gateway
  puts in the query string, and every transport implements it. A report of failed
  delivery for a send the library made releases that send from its restrictions and
  nothing else; a report carrying an unknown reference, or one the transport cannot
  read, is refused 422 `integration.callback.rejected`.
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
  account being deleted or erased is not suspended: each answers 409
  `identity.account.stateconflict` with `details.state` and, for a suspended account,
  `details.suspendedBy`. A subject no account bears answers 404
  `identity.account.notfound`. The audit trail records `identity.account.suspended` and
  `identity.account.reactivated` in the security category. `IAccounts` is the same pair
  of operations in process. The accounts table carries `restriction_held` for the
  restriction held while the account is suspended or deleting.
- A processing restriction is kept while the account passes through a deletion window, a
  takedown or a suspension: cancelling the deletion, reversing the takedown and
  reactivating the account each bring it back restricted, and a restriction decided
  while the account is suspended or deleting is held for when it returns, with
  `RestrictionChanged` delivered to the subscribers when it is decided.
- `POST /admin/accounts/{subject}/restriction/lift` lifts a processing restriction under
  `account:manage` and the `account:restrictionlift` step-up, judged after every other
  refusal (204): the account is active again, `RestrictionChanged` is delivered to every
  subject-event handler in the same transaction, and the audit trail records
  `privacy.restriction.lifted`. An account that is not restricted, including one holding
  a restriction while suspended or deleting, answers 409
  `identity.account.stateconflict`, and a subject no account bears 404
  `identity.account.notfound`. `IAccounts.LiftRestrictionAsync` is the same operation in
  process.
- `POST /admin/accounts/{subject}/delete/cancel` cancels a deletion inside its grace
  window on the subject's behalf under `account:manage` and the `account:deletioncancel`
  step-up, judged after every other refusal (204), whether the subject or an out-of-band
  erasure request began it; the account comes back as it stood and the audit trail
  records `identity.deletion.cancelled` naming the erasure request where one began the
  window. A takedown answers 409 `identity.takedown.active`, a closed window 422
  `identity.deletion.windowelapsed`, an account in no window 409
  `identity.account.stateconflict`, and a subject no account bears 404
  `identity.account.notfound`. `IAccounts.CancelDeletionAsync` is the same operation in
  process.
- `GET /admin/accounts/{subject}/photo` serves the photo an account shows to an
  administrator holding `account:manage`, as `image/jpeg` with
  `Cache-Control: no-store`. An account that shows none and one whose organizations
  withhold photos both answer 404 `identity.photo.notfound`; a subject no account bears
  answers 404 `identity.account.notfound`. `IAccounts.ReadPhotoAsync` is the same read in
  process.
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
  `POST /admin/erasures/{id}/complete` closes an erasure, a takedown (by its
  `takedownId`) or a restriction delivery whose retries were spent, an erasure's ledger
  line and erasures row with it, asks the `erasure:complete` step-up after every other
  refusal, and is audited as `privacy.erasure.completed` with the delivery's `kind` and
  the required subscribers that had not confirmed; one not yet failed is 409
  `privacy.erasure.notfailed`. An erasure's `id` is the identifier of its delivery; an
  identifier naming no delivery of the three kinds is 404 `privacy.erasure.notfound`,
  and the two reads stay erasures only. All three need `privacyrequest:manage`.
  `IErasures` is the same operations in process.
- `GET /admin/access?resourceType=...&resourceId=...` answers who can access a record:
  every live grant on it, on what contains it and on the whole organization, nearest
  first, each with its kind, holder, role, whether it denies and the container it sits
  on. It needs `grant:read` in the record's organization; `resourceType` `organization`
  asks for the whole of one. It also reports each holder a derivation confers the record
  on as a derived grant, with no `id`, evaluated over the rows of the relationship source
  the host declared, in one statement in the host's context; a derivation whose role
  allows nothing is not reported, and where `authz.reverselookup.budget` runs out first
  the answer carries `partial: true` and the relationships left unevaluated.
  `IAccessGate.WhoCanAccessAsync` is the same in process; given the host's
  `FilterSources` it evaluates the derivations over the rows handed in, and where no
  rows are handed in and no source is declared for a relationship reaching the record's
  type it refuses with `authz.derivation.sourcesmissing`. A type the model does not declare is
  refused 400 naming `resourceType`, and an organization identifier that is not a UUID
  400 naming `resourceId`; a record the deployment holds no registration for is refused
  403 `authz.denied`, recorded against no organization and counted, exactly as a caller
  without `grant:read` is refused, so the view tells no one whether a record is
  registered.
- Staff mailboxes are provisioned through `IMailServer`, which a deployment registers
  where its staff mail is hosted and which no package ships. A mailbox is owed
  `disabled` from its reservation, `enabled` while its holder is an active member of the
  administrative organization, and `disabled` otherwise; `MailboxPublisher` pushes
  whatever differs under a key that stays the same until the server confirms it, retries
  on the outbox schedule, and raises `degradation` naming the mailbox when the budget is
  spent. `MailboxReconciliation` compares the server's listing with what is owed and
  raises `degradation` on any difference without changing either side. The address is
  held encrypted under its holder's key and is erased with them.
- A mailbox push carries the mailbox's identifier (`MailboxPush.Mailbox`, a public
  `MailboxId`) and its address as an `EmailAddress`, and the mail server's listing
  answers, for each account, the identifier it carries (`HostedMailbox.Mailbox`), its
  address as an `EmailAddress`, or nothing where what the server lists does not read
  as one, and whether it is enabled. An `IMailServer` answers
  `integration.mailserver.conflict` where a push meets, at the mailbox's name, an
  account that does not carry that identifier, and changes nothing.
  Reconciliation compares each mailbox with the account listed under its identifier,
  reads the listed address in its canonical form, and counts every account carrying no
  identifier of a mailbox the library holds.
- The mail server in use is chosen once, as the deployment starts, and read through
  `IMailServerInUse`: the host's own `IMailServer` where it registers one, and none
  otherwise, answered as `identity.mailbox.notfound`. Asking before the start has chosen
  is a fault.
- The library ships a JMAP adapter for the mail server, used where the host registers
  no `IMailServer` and `integration.mailserver.endpoint` is set when the application
  starts; a change of the key takes effect at the next start. Each call is one JMAP
  request to `<endpoint>/jmap` presenting the mail server's management key, read at the
  start through the new `ISecretSource.ReadMailServerSecretAsync` into the key ring
  (`IKeyRing.BorrowMailServerSecret`); a key that cannot be read stops the start with
  `model.startup.secretunavailable` naming `mailServerSecret`. A mailbox is created as a
  user account carrying the mailbox's identifier as its description, a push changes the
  `authenticate` permission alone, an account the library did not create is answered
  `integration.mailserver.conflict` and left as it is, and the app-password calls carry
  the person's token.
- Each mailbox push attempt is counted and committed before it is made. A push the mail
  server answers `integration.mailserver.conflict` is marked failed at once and raises
  `degradation` scoped `mailbox.conflict:<mailbox id>` with `{ mailbox, state }`. A push
  marked failed, for spent attempts or a conflict, is begun again under the same key a
  day later for as long as its state is owed, and raises its alert again if that run
  fails. A removal of a mailbox no push of which was ever attempted is confirmed
  without being sent. The `mailboxes` table gains `attempted`, true for every row
  written before this release.
- `AlertRaised.Scope` carries the scope an alert is raised under, where it has one
  (a mailbox's push or conflict, reconciliation, a send channel, the blocklist fallback,
  the clock or certificate reference, the envelope rotation), and the deduplication key
  is the condition, that scope and the account or actor, so an alert under one scope
  never folds into one under another. The `raised_alerts` table gains `scope`, empty for
  every row written before this release.
- A mailbox released with its revoked invitation, or replaced, is owed its removal from
  the instant its row records and no longer stands for its address, so a new mailbox may
  be reserved there; it keeps its fingerprint until its last holder is erased. A push
  that creates or changes a mailbox waits, spending no attempt, while another mailbox at
  its address is owed a removal the mail server has not confirmed; a removal never
  waits. Once the mail server confirms the removal of a released reservation, its
  address key is overwritten and its fingerprint neutralised, and the row remains. The
  `mailboxes` column `released_at` is renamed `removal_owed_at`.
- An invitation of a corporate address whose mailbox someone has held, the person
  invited included, is refused `409` `identity.invitation.mailboxheld` unless its body
  names `formerMailbox`, `transfer` (the invitee is reserved the old mailbox and its
  mail) or `replace` (the old mailbox is owed its removal and a new one is reserved),
  with a `reason` of 1 to 1024 characters; the issue's audit record carries both. An
  address whose last holder was erased is invited as one never held. A `formerMailbox`
  where no held mailbox stands for the address answers `422` `api.request.invalid`
  naming `formerMailbox`. `InvitationRequest` gains `FormerMailbox` and `Reason`, and
  `ErrorCodes` gains `InvitationMailboxHeld` and `RequestInvalid`.
- From the break-glass session, or a session another application opened from it, each
  of `password:set`, `identifier:add`, `username:change`, `factor:enrol`,
  `provider:link`, `recoverycodes:generate`, `mailcredential:create`,
  `account:deactivate` and `account:delete` is refused `403` `authz.denied` before
  anything is read, so the refusal is the same whatever the reserved account holds or
  lacks: the upgrade of a credential it does not hold, a factor its policy does not
  admit, recovery codes and an app password included.
- The app-password operations answer `404` `identity.mailbox.notfound` where the account
  holds no mailbox the mail server is told to enable, is neither `active` nor
  `restricted`, or the deployment registers no mail server; a context naming no account
  is still `authz.denied`. A restricted account lists and revokes its app passwords and
  is refused creation with `authz.restricted`.
- A deployment that registers no `IMailTransport` or no `ISmsTransport` does not start:
  the refusal is `model.startup.declarationmissing` naming `mailTransport` or
  `smsTransport`. The register's mail server row applies where a mail server is
  registered; `integration.mail.endpoint` alone no longer makes it true.
- A deployment that registers no `ISecretSource` does not start: the refusal is
  `model.startup.declarationmissing`, `details.key` `secretSource`, made as the start
  begins and before any secret is read, whether or not a social provider is declared.
- `AddJanus` no longer takes the key-encryption keys, the fingerprint keys or the
  maintenance credential: it takes the connection, the declaration and the
  `ApplicationKind`. The three are read through `ISecretSource` as the application
  starts, into the key ring, and are lent through the new `IKeyRing` members
  `BorrowKeyEncryptionKeys`, `BorrowKeyEncryptionKey`, `BorrowFingerprintKeys` and
  `BorrowMaintenanceCredential`; a version the ring does not hold is answered
  `model.startup.secretunavailable` with `details.key` and `details.version`. The
  `ISecretSource` members that read them return a `Result`, and one the source cannot
  answer, a fingerprint key version shorter than 32 bytes or an empty maintenance
  credential stops the start with `model.startup.secretunavailable` naming
  `keyEncryptionKeys`, `fingerprintKeys` or `maintenanceCredential`. A `Janus.Cli`
  command reads its key document into a key ring of its own and clears it when it ends.
- The signing keys rotate with no timer and no job: the first read of the keys, a token
  signed, a request for the key set or a validation, that finds a change due makes it.
  The next key is made and published at `token.signing.rotation` less five minutes and
  signs once the cadence has passed and it has been published five minutes; the first
  key of an empty database is made at the start, before the provider is built. A
  replaced key stays published for the longest `oidc.accesstoken.lifetime` it signed
  under plus five minutes, stored at its replacement; the first read after that retires
  it, disposing its private key and removing it from the database, and its public key is
  kept, unpublished, for 365 days so the refresh tokens it signed are still accepted. An
  access token validates only under a published key. Each change commits in a
  transaction of its own, and of two instances finding one due, one makes it.
  `token.signing.algorithm` admits `ES256` alone; `configure` refuses any other value
  with `config.value.notallowed`. The migration takes existing keys as having signed
  from when they were made, under the ceiling of `oidc.accesstoken.lifetime`.
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
  where the group would contain itself, 422 `api.request.invalid` naming `subjectId` for
  a member group that does not exist or belongs to another organization). All ask
  `group:manage` in the group's organization and a reason, and are recorded in the audit
  trail; a change of members also needs step-up, and `system:administer` where the group
  reaches a role carrying it. `IGroups` is the same set of operations in process. A
  group the deployment holds no row for belongs to no organization, so every caller is
  refused it with 403 `authz.denied`, as a caller without `group:manage` is.
- `GET /admin/roles` reads every role with its permissions, `POST /admin/roles` creates
  a role or gives an existing one the permissions stated, and
  `DELETE /admin/roles/{name}` removes one no grant, derivation or standing invitation
  (neither acknowledged nor revoked, expired or not) names (409
  `authz.role.inuse` otherwise, 404 `authz.role.notfound` for a role the deployment
  does not hold). All ask `role:manage` in the administrative
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
  `authz.grant.notfound` only to a caller holding `grant:manage` where it is scoped. A
  grant naming a role the deployment does not hold, or a group that does not exist or
  belongs to another organization, answers 422 `authz.grant.unresolved` naming `role`
  or `subjectId`.
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
  `restriction:grant`, behind step-up and with a reason. A name the set does not hold
  answers 404 `auth.restriction.notfound` to a read, a deletion and a grant, and a
  reason past 1024 characters is refused with `api.request.malformed` naming `reason`.
  `IRestrictionSet` is the same set of operations in process.
- A restriction's name is 1 to 64 lower-case letters and digits separated by single
  `.`, `-` or `_`. `PUT /admin/restrictions/{name}` with a name outside that rule is
  refused with `config.value.notallowed` before anything is written, and a stored set
  holding one does not read.
- A restriction names the channel it governs, `sms`, `email` or `any` (the default, and
  what a stored restriction without one reads as), and counts and refuses only the sends
  on that channel: `sms.destination` and `sms.source` ship as `sms`, `email.destination`
  as `email` and `notification.destination` as `any`. `/admin/restrictions` reads and
  writes `channel`, and changing it to anything but `any` is a loosening. A security
  notice to an address or number its owner holds answers only to restrictions whose
  purpose is `notification`, an alert answers to no restriction, and a send no request
  asked for (a reminder, a repeated loss-report notice, a lapsed privacy request, an
  alert) carries no source, so no `source` restriction counts it. The new-device check
  code is counted under the source of the sign-in that asked for it.
- A deployment declares `LandingOrigins`, the origin of the authentication application
  and of the account application, where every link the library sends lands. Without
  it the deployment does not start (`model.startup.declarationmissing`,
  `details.key` `landingOrigins.authentication` or `landingOrigins.account`); an origin
  that is not an `https` origin a registered browser client returns to, or an
  authentication origin that is not the sign-in address's, is refused with
  `model.startup.declarationinvalid` under the same key.
- An alert's deduplication claim is committed before the alert is sent, and the
  `alert-dispatch` pass removes a carried condition afterwards, so no alert transport
  is called while a transaction is open. A delivery that fails after the claim is not
  repeated inside the same window.
- A recovery link, the enrolment link an approver sends and an invitation link are sent
  and drawn under the `signin` purpose, as a sign-in link is, and answer to the restrictions it answers to; no `notification`
  restriction counts them, so `notification.destination` counts notices alone.
- A recovery-code set whose every reminder was refused stays owed its reminder, where
  it was closed as reminded; a set whose account holds no channel a reminder can reach
  is closed as before.
- A text-message template naming a place the library does not fill, `{token}` among
  them, stops startup with `model.startup.declarationinvalid` (`details.declaration` the
  message kind, `details.field` the place), where it was sent with the brace in it.
- A privacy-deadline alert names the request's `type` and `status` as chapter 10 spells
  them (`rectification`, `deemed-refused-by-lapse`), and a text message naming either
  place is measured at those spellings.
- Every link the library sends is a whole address, `<origin>/link#<kind>.<token>`, on
  the declared landing origin of the application its kind belongs to, with the token in
  the fragment. Templates fill it with the new `{link}` place; the `{token}` place is
  retired. A text message whose template carries a link is budgeted at two segments
  (306 units in the default alphabet, 134 outside it), every other at one.
- What a destination has been sent is kept apart from what an account, a source, the
  deployment or a host key has, each for the longest interval of the restrictions now
  declared on its own kind of key, so a longer source restriction no longer keeps a
  destination's record. A record past that interval is deleted before the next send's
  counters are read and by the `expiry-sweep` job, whether or not its key is sent to
  again.
- `GET /admin/config/{key}` reads one runtime key under `config:read`: its value in
  force and its default in the key's own JSON type, whether it is protected, and which
  way it loosens (`increase`, `decrease` or `any-change`). `PUT /admin/config/{key}`
  changes it under `config:manage`; a loosening also needs `system:administer` in the
  administrative organization, step-up and a reason, a tightening asks nothing more, a
  protected key is refused with 422 `config.key.protected`, and the alert destination
  keys tell the destinations they replace. The route also serves
  `retention.<category>` for each category the host declared, read with its floor as
  the default and changed above that floor, shortening being the loosening; any other
  name is refused with 400 naming `key`. `IConfigurationAdministration` is the same
  operation in process.
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
  where the refused type is not concealed (`IAccessGate.ResolveOwnAsync`). A caller
  without `audit:read` is answered with the gate's own refusal, whose correlation
  identifier resolves to the denial recorded for it.
- `POST /admin/accounts/{subject}/sessions/revoke` ends every session of one account
  under `session:revoke-account`, and `POST /admin/sessions/revoke-all` ends every
  session in the deployment under `session:revoke`, the caller's own included, each
  permission held in the administrative organization. Both answer 204. They are the
  `account:sessionsrevoke` and `session:revokeall` step-up actions, judged after every
  other refusal (403 `auth.stepup.required`), and a subject no account bears answers 404
  `identity.account.notfound`. `ISessions.RevokeAccountAsync` and
  `ISessions.RevokeEveryAsync` are the same in process, take the caller's session and no
  organization.
- The minor takedown, as `ITakedowns` and `POST /admin/accounts/{subject}/takedown`:
  under `takedown:execute` and step-up, one transaction suspends the account into its
  `takedown.grace` window, ends every session of it, records the trigger and the reason
  (1 to 1024 characters after trimming, else 400 `api.request.malformed`), and writes
  the `TakedownExecuted` delivery on which the host stops its own processing for the
  subject. An account `active`, `restricted`, `suspended`, or already deleting by its
  own or an out-of-band request is taken down, holding the state it was in: the accounts
  table carries `suspension_held`, `deletion_held` and `deletion_held_since`. A takedown
  of a running deletion is erased at the earlier of that window's end and its own. A
  second takedown answers 409 `identity.takedown.active`, an erased account 409
  `identity.account.stateconflict`, and a subject no account bears 404
  `identity.account.notfound`. Step-up is judged after every other refusal. The answer
  carries `takedownId` and `erasureDue`. `AccountSuspended` is written in the trigger's
  transaction at every trigger, whatever state the account held, and a refusal to write
  it fails the trigger; no deletion notice and no `AccountDeletionRequested` is sent.
- `GET /admin/accounts/{subject}/takedown` reads the latest takedown of an account: when
  it was triggered, when its erasure runs, whether it was reversed (`reversed`, with
  `erasureDue` null), and which registered subscriber has confirmed it and when. An
  account never taken down answers 404 `identity.takedown.notfound`.
- `POST /admin/accounts/{subject}/takedown/reverse` restores a taken down account inside
  its window to the state it held at the trigger, with a reason, and writes
  `TakedownReversed` in its transaction: the deletion it was in, with its origin and
  start; else the suspension it was in, with its origin; else restricted where a
  restriction is held; else active. After the window it answers 422
  `identity.takedown.windowelapsed`, and an account holding no takedown 404
  `identity.takedown.notfound`. The reversal and the erasure at the window's end each
  hold the account row, so of the two at the boundary the second waits and refuses.
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
- The organization erasure writes its `MembershipChanged` and `OrganizationErased`
  events in its own transaction, so an event that cannot be written leaves the
  organization unerased, and its `identity.organization.erased` record is filed under
  the organization.
- An organization's erasure erases no account, so `ErasureReason` holds
  `erasure-request` and `minor-takedown` only; `organization-erasure` is gone from it
  and from the erasures and outbox tables.
- The organization erasure replaces every domain the organization listed with its
  identifier, as it does the name, and marks each one still listed removed at the
  erasure.
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
  absolute `https` address with a host, or `http` on a loopback IP literal (`127.0.0.1`,
  `[::1]`), does not start, failing `model.startup.redirectclient` naming the client;
  `register-client` refuses such an address the same way. Registration resolves the
  client identifier it is given against the registry as it takes it, and what a
  completed registration reports as the return is the origin (scheme, host and port) of
  the address that client registered; no step after the first takes a destination at
  all. An identifier the registry does not hold registers the person
  exactly as a registered one does and stores the deployment's default client, named in
  the protected key `redirect.defaultclient` and read against the registry at startup,
  so the completion returns the person to it. A deployment that names no default starts,
  and such a completion returns nothing.
- The session a registration's terms step signs the person in on keeps the client the
  registration captured, and `GET /auth/session` and `ISessions.ReadAsync` answer its
  return as `landing` (`SessionDetail.Landing`), absent on every other session, so the
  done step reads it from the session and never from a request.
- An account shows a photo. `GET`, `PUT` and `DELETE /account/photo` read it, replace it
  and give it up, and the image is served through the session gate as `image/jpeg` from
  no address a cache could share. Availability is the policy field `photos`, off by
  default and resolved as every policy field is: an account of no organization follows
  the system policy, and an account of several shows a photo only where every one of
  them does; one whose policy withholds photos is answered as an account with no photo,
  404 `identity.photo.notfound`. Bootstrap writes the administrative organization's
  `photos` off, and a change that turns `photos` on while the deployment declares no
  codec is refused with 422 `config.value.notallowed`, `details.field` `photos` and
  `details.requires` `imageCodec`. The library reads no image itself: a deployment
  declares an `ImageCodec`, which decides by content what an upload is, holds it to
  `photo.maxdimension` and answers the JPEG that is stored. The photo is held in a table
  and a port of its own, encrypted under the subject's own key like any other personal
  field: nothing that reads an account reads image bytes, a dump yields no photograph,
  and erasure of the key leaves the image unrecoverable. A deployment whose stored
  policy shows photos, the system's or an organization's, and which declared no codec
  does not start.
- Every runtime configuration change goes through one operation that classifies it,
  gates it and writes it down. A configuration key loosens the way its row states; where
  a row states nothing, a key with only a ceiling loosens upward, a key with only a
  floor loosens downward, and a flag loosens away from its default. A change that
  loosens the deployment, and any change to a key that has no direction, needs the
  step-up gate met and a written reason, and a loosening, the named restriction set and
  the alert destinations included, is refused unless the caller also holds
  `system:administer` in the administrative organization; a tightening asks nothing
  more. Both are recorded with who made it, the key, the value before and after, the
  direction, the reason and the time, and the record reads back by setting, by actor
  and by the system principal that made it, whose name a change it made carries.
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
- A text-message template naming `{outstanding}` is measured at the width of every
  registered required subject-event subscriber's name as the alert carries them, and
  one naming `{key}` at the widest key a change can name, an organization's key at the
  width of its identifier and a category's retention key at the longest category the
  host declared.
- Publishing an event answers for itself. An operation records its event inside the
  transaction that made it true and commits nothing it could not publish, so a change
  never reaches the database without its event reaching a consumer. Every method of the
  public contract returns an outcome, `IEvents.PublishAsync` included.
- Every message the library sends is written to its own outbox table inside the
  transaction that made it necessary, carried from that row, and removed once a
  transport has taken it. An operation that fails sends nothing, a message undertaken by
  one that succeeds is not lost with the process, and a transport that refuses leaves
  the message waiting rather than dropped. The row holds the whole of the message
  encrypted under a key of its own, wrapped under the deployment's data key.
- Startup refuses a declaration whose encrypted field names no subject column, a column
  the declared type does not hold, or one holding something that is not a subject.
  Ciphertext an erasure could not reach stops the deployment instead of reaching
  production.
- An encrypted field names the data category it holds: `EncryptedFieldDeclaration`
  gains `Category` and the builder's `Encrypted` takes it. Startup refuses a field whose
  category no purpose declared on its type names, with
  `model.startup.declarationinvalid`, `details.declaration` the type and
  `details.field` the field.
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
  account is known at, its memberships, the groups it is a member of itself (a
  `group-memberships` section after `membership-acknowledgements`, each with its name
  and organization), every grant naming it in every organization, live, expired or
  revoked, with `revokedAt` on a revoked one and never who granted or revoked it or why,
  the assurance it can reach,
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
- A subject-event subscriber's name, and the name of the governing document a purpose
  names, is 1 to 64 lower-case letters and digits separated by single `.`, `-` or `_`.
  A deployment that registers or declares one outside that rule does not start: the
  failure is `model.startup.declarationinvalid`, `details.declaration` naming the
  subscriber or the purpose and `details.field` `name` or `document`.
- A data subject request enters a queue with a statutory clock on it. A subject submits
  a restriction or a rectification for themselves at `POST /privacy/requests` and is
  answered with the request identifier, the receipt timestamp and the date the decision
  is due by; an authorised human enters a request that arrived out of band at `POST
  /admin/privacy/requests`, recording how it arrived, what confirmed the requester is
  the subject, and the date it reached the company. The deadline is six working days
  counted on the deployment's own week (`privacy.workingdays`), its holidays as
  currently listed (`privacy.holidays`) and its zone (`privacy.calendar.timezone`),
  never on a Monday to Friday assumption. Undecided requests raise a Normal alert
  `privacy.request.warninglead` before the deadline and a High alert on the deadline
  day, without anyone watching; a restriction still undecided when the deadline passes
  is granted and the account is restricted, and a request the system cannot grant by
  itself is recorded as deemed refused by lapse, with the subject told honestly and the
  record kept. Fulfilling a restriction restricts the account and tells the registered
  subscribers; fulfilling an out-of-band erasure follows the account's state: an active
  or restricted account starts the deletion grace window, a suspended one starts it
  holding the suspension, which a cancellation returns, an account already in its window
  keeps the window running with its start, an erased one changes nothing, and a window
  that cannot start fails the fulfilment and leaves the request open. Deciding a request
  tells its three refusals apart for the member of staff working the queue: 403
  `authz.denied` without the permission, 404 `privacy.request.notfound` for an
  identifier naming no request, and 409 `privacy.request.decided` where a decision
  already stands.
- A privacy request's `detail`, an out-of-band entry's `channel` and
  `identityConfirmation`, and a refusal's `reason` are held trimmed, and one that is
  blank or longer than 1024 characters after trimming is refused with 400
  `api.request.malformed` naming it, at the endpoint and by `IPrivacyRequests` for an
  in-process caller alike; a refusal's reason was an exception there before.
- Fulfilling a privacy request, of any type, is the step-up action
  `privacyrequest:fulfil` (`StepUpAction.PrivacyRequestFulfil`), in the policy's
  `gates` like every other action: `IPrivacyRequests.FulfilAsync` takes the session it is
  judged on and asks it after every other refusal, and a session that has not proved it
  is answered 403 `auth.stepup.required` with nothing changed. Refusing a request is not
  gated.
- Withdrawing a consent the subject never gave, or an objection the subject never made,
  is answered as the withdrawal (204) and records, announces and audits nothing, where it
  was 403 `authz.denied`; withdrawing an objection for a purpose on a basis that takes
  none is 422 `privacy.purpose.notobjectable`. An objection made before any version of
  the privacy notice is published is 409 `privacy.notice.unpublished`, where it was 403
  `authz.denied`.
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
  up. A terms step whose terms version or notice version is blank creates no account and
  is refused with `identity.registration.incomplete`.
- A subject can read and change their own consents and objections through a privacy
  dashboard: `GET /privacy/consents`, `POST /privacy/consents/{purpose}/grant` and
  `.../withdraw`, `GET /privacy/objections`, `POST /privacy/objections/{purpose}` and
  `DELETE /privacy/objections/{purpose}`. Each record names the purpose, the document it
  was given against (`document`, the privacy notice for an objection and for a purpose
  naming none) and the version of it that was shown, where the decision was made and
  when; a record written before the document was named reads as given against the
  privacy notice. A grant made on the subject's own pages records `dashboard`, and one
  answering the prompt a material revision raised, over a consent the revision ended and
  the subject never took back, records `reconsent`. `IConsents.GrantAsync` applies the
  same rule, judged inside the grant's transaction, to a grant named `dashboard`, so a
  host granting through the contract is answered as the endpoint is; any other mechanism
  is recorded as named. Withdrawal takes the one request granting took and nothing
  stands in its way. A purpose that rests on a basis other than consent takes no consent
  record, and one whose basis carries no right to object refuses the objection by name.
  A purpose declaration names the legal document that governs its consent, and the
  privacy notice governs the purposes that name none. A consent is recorded against that
  document and its version, and publishing a material revision of it ends the live
  consents of the purposes that name it, and of no others, given against another version
  or another document, so the subject is asked again, and leaves the records standing as
  evidence. A purpose declared on two types against two documents fails startup.
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
  expiry, an authorization code past its lifetime and a refresh token past the expiry
  its session gave it each go in one call.
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
  client the registry does not hold is answered 400, and a pushed `redirect_uri` that is
  not the client's registered one is refused 400 `invalid_request` with no description
  and no `request_uri`; a push naming none takes the registered one. Every access token
  names the client it was issued to in `aud`, beside `client_id`, as RFC 9068 has it, so
  a party verifying a token offline can refuse one issued to any other client. The token
  and userinfo routes are carried on the machine profile, a second pipeline profile that
  reads no cookie and asks for no synchronizer token, and refuses a request that arrives
  with one. `IOidc` carries what a host calls in process and the library answers over
  HTTP, `ClaimsAsync` and `KeysAsync`; `ClaimsAsync` takes the caller's `AccessContext`
  and answers the claims of its effective identity alone, and `authz.denied` where it
  names none. `KeysAsync`, the read of the published key set, is no operation: it takes
  no access context, meets no gate and answers the public keys `GET /oidc/jwks`
  publishes and nothing else.
- An application establishes its own session from the one the authentication application
  holds without a line of host code: `GET /auth/signon` forwards the browser to the
  provider with proof key and a state bound to its pre-authentication session,
  `GET /auth/signon/return` trades the code on the server's own connection and drops
  what came back, and the browser goes on to where it was heading with a session of this
  application's own. The sign-on pushes its request on the back channel as it exchanges
  its code, so the browser carries nothing of the request, and the back-channel request
  is made with the client `identity-signon`, which a host configures as it configures
  any other client. A host declares the client identifier this application is registered
  under, which startup requires; the secret it presents is the one the library drew for
  that client, read from the registry at each request and kept nowhere. The address of
  the authentication application is declared beside its sign-in screen.
- The OpenID Connect provider keeps its registered clients and its signing keys in
  tables of the library's schema, and the protocol library keeps its own records in
  three more: `oidc_authorizations`, `oidc_tokens` and `oidc_scopes`. Codes and refresh
  tokens are encrypted under a key derived from the deployment's key-encryption key, so
  every instance reads what any other wrote, a restart loses nothing, and a key rotation
  leaves the ones already issued readable. A signing key's private half is wrapped under
  the deployment's data key.
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
  previous destinations first. A condition is written in the transaction of the
  operation that raised it, and one that cannot be written fails that operation; it is
  carried after that transaction commits, oldest first and once, so one raised by an
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
  a stored value that does not read under its key (one a tightened floor or ceiling no
  longer admits included) is a fault: the read throws `InvalidOperationException`
  naming the key and never the stored text, and nothing in the library puts a default
  or any other value in its place, so a request fails as `system.fault` and a job fails
  its run. The store only reads: it has no write, and a runtime value is put in force
  through the management application's one writer alone, with its step-up, its reason
  and its record.
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
  required, so a caller traces it as it traces any other. The member is named as the
  request writes it, with no `$` root and no list index: a member inside another by the
  names from the body's top joined by dots, and an element of a list, or a member inside
  one, by the list's name.
- The browser profile admits each source address `abuse.source.ratelimit` requests a
  minute (300 by default, sliding) and answers the rest 429 `auth.throttled` with
  `retryAt`, before any session is looked up. Each instance of a deployment counts on
  its own, by the connection address after the proxies the host trusts, so a
  deployment of several instances sets the key to each one's share.
- A host's restriction key supplier is asked once for each key name when a send is
  judged, however many restrictions count under that key.
- Every throttled answer (sign-in, sign-in link and email code, recovery, break-glass
  and the recovery approval limits) carries `retryAt` in its details and a matching
  `Retry-After` header. The progressive delay runs from the failure that earned it and
  grows when failures follow one another, so `retryAt` is the instant the next attempt
  is looked at. The delay is the one the count that failure wrote earns: decay forgives
  failures still to come and never shortens a delay already running.
- `Error.Throttled` in `Janus.Core` builds the `auth.throttled` refusal, its one detail
  `retryAt` the instant in UTC. Every throttle of the library answers through it, the
  export limits and the per-source request limit included.
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
  and never from the source's delay; the exemption spares it from being held, not from
  being counted, so its failures still hold other browsers and raise the account's
  alert. Every token a browser carries is looked up whether or not the identifier
  resolves. `IAuthentication.BeginAsync` has one form, which takes both tokens; an
  in-process caller passes neither. A throttled source reaches no sign-in provider.
- A sign-in link, email code or recovery ask that sends nothing is judged and counted
  against the sending restrictions as its message would be, so it is answered as a sent
  one is, whether or not an account holds the address.
- Startup refuses a purpose named for the hosting or its cross-border transfer
  (`hosting`, `transfer`, `hosting-transfer`, `cross-border-transfer`, compared ignoring
  case and every character other than a letter or a digit, so `Cross Border Transfer`
  and `hosting_transfer` are among them) that rests on a consent basis, with the new
  code `model.purpose.hostingconsent` (`ErrorCodes.StartupHostingConsent`),
  `details.key` naming `<type>.<purpose>`.
- A runtime setting changed in process is refused without a reason,
  `config.change.reasonrequired` naming the key, whichever way it moves, as over HTTP;
  an edit of the named restriction set, a tightening included, is refused the same way,
  naming `restrictions`, and so is a restriction grant. A reason is 1 to 1024 characters
  after trimming: a blank one is refused with that code, and one past 1024 characters
  with `api.request.malformed` naming `reason`, at `PUT /admin/config/{key}` and in
  process alike.
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
  identifier is compared under, stored beside it in `organizations.canonical_name`, which
  the database requires on every row; a name mixing scripts within a word is refused as
  `identity.identifier.mixedscript`, and one the key reduces to nothing as
  `api.request.malformed`.
- A management or account request missing a member its body requires is refused before
  anything else is judged: a missing reason by its own code
  (`authz.grant.reasonrequired`, `config.change.reasonrequired`,
  `auth.recovery.reasonrequired`), any other member as `api.request.malformed` naming it.
- `bootstrap` requires the first administrator's date of birth as `--dateofbirth`
  (`yyyy-MM-dd`) and refuses an administrator under eighteen as
  `identity.profile.underage` before anything is written, where
  `registration.adultaffirmation` is `required`; the emergency account and the restore
  canary record no answer.
- Finding who holds access to a record reads the live grants on its ancestors through
  their index, and `organization_domains.domain` carries the `identity_ci` collation.
- `groups.name` and `authenticators.label` carry the `identity_ci` collation, so an
  organization's groups sort without regard to case and a credential label held in
  other capitals for the same kind is refused with `auth.credential.labelinvalid`, at
  a rename and at an enrolment alike, exactly where the unique index would refuse it.
- The register finding for unstated security measures is spelled
  `organizational-measures-missing`, as the reference spells it.
- A username change, an identifier's addition or removal, a recovery approval and the
  end of a membership judge the step-up after every other refusal they give before
  their transaction, so a session whose proof is no longer recent hears the refusal the
  change meets.
- A group change whose group was removed while it waited is refused `authz.denied`, as
  one naming no group is, rather than `api.request.malformed`.
- A takedown refused under the account's lock answers for the state it found there,
  `identity.takedown.active` or `identity.account.stateconflict`, rather than
  `authz.denied`.
- `POST /account/deactivate` and `POST /account/delete` refuse an account whose state
  does not admit them with `identity.account.stateconflict` naming the state, and a
  restricted account's deactivation with `authz.restricted` from the gate, before
  the step-up, rather than with `authz.denied`.
- An erasure fulfilled while the account enters its deletion window by another road is
  recorded fulfilled against that window, as one found already deleting is, rather
  than refused `identity.account.stateconflict`.
- A privacy request entered out of band refuses a `detail` given blank or past 1024
  characters after trimming, and keeps one within the bound trimmed. An absent or
  `null` `detail` records none: `PrivacyRequestEntry.Detail` and
  `PrivacyRequest.Detail` are nullable, the `detail` column of `privacy_requests`
  takes null, and an empty text is never stored for none.
- The declared lawful bases are held in `identity.lawful_bases` (`key`, `label` and
  the four flags). The start writes the table before the server serves, in one
  transaction that holds it: each declared basis is inserted or updated by its key and
  every other row is deleted, so of two starts the later list stands whole.
  `LawfulBasisDeclaration` gains `Label`, `LawfulBases.Default` carries the labels of
  chapter 10 section 5.7, and the records of processing emit the label, not the key. A
  list naming one key twice, or a basis with an empty key or label, fails startup with
  `model.startup.declarationinvalid`, `details.declaration` `lawfulBases` and
  `details.field` `key` or `label`.
- `rotate-fingerprint-key --sealed` waits for everything that lapses on a clock of
  its own: it is refused with `model.rotation.notready` and `pending` while a sign-in
  in progress carries a fingerprint computed under a previous version, as it is for a
  held username, an unlapsed reservation and an abuse count that still counts. At
  retirement it deletes unspent restriction credit and released username holds under a
  previous version, and nothing else: a migration revokes the maintenance role's
  `DELETE` on the abuse ledgers and on the sign-ins in progress, whose versions it
  still reads.
- A value wrapped under a key-encryption key version the application does not hold
  fails to unwrap as a fault carrying `model.startup.secretunavailable`,
  `details.key` `keyEncryptionKeys` and `details.version`: a request is answered
  `system.fault` and the log names the code, the key and the version; a job fails its
  run; a command exits 1 with the code. The erased value is read first, so an erased
  key is no fault. The start is refused with the same code, naming the lowest such
  version, where a subject key that is not erased stands under a version the secret
  source does not supply.
- `PUT /admin/compliance/assessments` reads the measures as
  `organizationalSecurityMeasures`, and holds `dataOwner` and it to the bound of free
  text: each is trimmed, refused 400 `api.request.malformed` naming the member where
  blank or past 1024 characters, at the endpoint and by `IProcessingRecords` for a
  caller in process, and cleared where the statement omits it.
- `GET /privacy/documents/{document}`, `POST /admin/documents/{document}/versions`
  and the translation route refuse a `{document}` that is not a document name (1 to
  64 lower-case letters and digits separated by single `.`, `-` or `_`) with 400
  `api.request.malformed` naming `document`: nothing is read or published, and no
  condition is raised under the name.
- A privacy request entered out of band for a subject no account bears is refused
  422 `api.request.invalid` naming `subject`. The fulfilment of an erasure refuses
  nothing on the account's state or existence: it no longer answers
  `identity.account.notfound`, and a deletion that will not begin is a fault. An
  erasure of the reserved `emergency` account is refused 403 `authz.denied` once the
  account is read, before the step-up.
- A maintenance log entry's `note`, where one is given, is 1 to 1024 characters after
  trimming, refused `api.request.malformed` otherwise, and kept trimmed.
- A recovery approval refuses a reason or a `channelUsed` past 1024 characters after
  trimming, and a blank `channelUsed`, at the endpoint before any permission is asked
  and in the service before the step-up.
- The organization and invitation endpoints refuse a name or a reason past 1024
  characters after trimming, and an invitation's reason given without a former
  mailbox, before any permission is asked.
- The grant and group endpoints refuse a reason or a group name past 1024 characters
  after trimming, and a blank grant reason, before any permission is asked.
- `PUT /admin/compliance/licences` takes the list of records itself as its body, and
  two records under one identifier are refused `api.request.invalid` naming `id`.
- A session begun from a sign-in holds the account while it begins, so an account
  suspended or set to deletion at the same moment is left with no session begun after
  its sessions were ended.
- Callbacks from one source are counted against `integration.callback.ratelimit`, and
  their rejections against the alert's threshold, with the source's callbacks held, so a
  burst admits no more than the limit.
- An address no account holds, or one whose holder is told of a duplicate, is judged
  with its notices held, so asks at the same moment send one notice in the window.
- Failures counted against one throttle scope at the same moment, and a success that
  clears the account's count, each act on the counter as committed, so every failure is
  counted.
- Sends counted, credit granted or spent and sends released at the same moment each
  change the sending ledger's rows as they then stand, so no count or credit is lost and
  a send is released once.
- A key rotation, `rotate-kek` and `rotate-fingerprint-key` alike, starts, takes each
  batch, completes and retires with its progress held, so two runs at once start it
  once, count each value once and record one completion and one retirement.
- A consent's grant and withdrawal and an objection's withdrawal are decided with the
  subject's records held, so two withdrawals at once announce and record one.
- A failed delivery is completed by hand under a lock on its row, so two operators at
  once close it once and record one completion.
- A subject's export is counted against `privacy.export.ratelimit` with the subject's
  exports held, so exports at once never pass the limit together.
- A privacy request is queued with the subject's requests of its type held, so two
  submitted or entered at once queue one and send one receipt.
- A privacy request is fulfilled, refused or carried by the deadline sweep under a
  lock on its row, so two decisions at once never both stand and a request decided
  while the sweep ran is not lapsed.
- An edit of one sending restriction is made on the set read again under its row's
  lock, so an edit of another restriction at the same moment is never written over.
- A refresh of a materialised derivation is made with the organization's tree of
  records held, so two refreshes at once write each grant once.
- An export is counted against the hour and recorded with its actor's exports held,
  so exports at once never pass `exfiltration.export.ratelimit` together.
- A change of a group's members, a group's removal and a grant to a group are decided
  with the organization's groups held, so two nestings at once never close a cycle, a
  member is judged on what the group holds as committed, and a group is never removed
  while a grant is given to it.
- A role's definition and removal, a grant's writing and revocation, and an
  invitation's issue are decided under a lock on the role's row, so two definitions at
  once never merge, a grant is judged on what its role allows as committed, two grants
  saying one thing at once write one, and nothing comes to name a removed role.
- A membership's end, its attachment and the erasure of its organization read the
  memberships under locks on their rows, so two ends at once end a membership once
  and announce it once.
- An organization's deletion, its cancellation, its erasure at the window's end, an
  invitation's issue and an acknowledgement into it are decided on its row under a
  lock, so a cancellation and the erasure never both stand and an organization on
  its way out takes no new invitation or member.
- An invitation's opening, revocation, acknowledgement and registration are decided
  on its row under a lock, so its link attaches to one account or registration, a
  revocation and an acknowledgement at once never both succeed, and a registration
  completing never writes a revocation over.
- A code, a link's press and the displaced address's confirmation of an identifier's
  change are judged on the verification under a lock on its row, so every wrong code
  of many at once is counted, a code and a confirmation at once apply the change, and
  a change abandoned meanwhile is refused `auth.code.invalid`.
- An integrated acknowledgement's corporate address and its retirement at the
  membership's end are taken on and given up under the lock on the account's
  identifiers, so a promotion at the same moment never leaves two primaries and the
  set told of the change is the set as it then stands.
- An identifier's addition, promotion, removal, undo and backup setting, and a
  username's choice, are decided again on the account's identifiers under a lock, so
  two changes at once leave one primary of a kind and no more of a kind than the
  maximum, and the second is refused with the code the set as it then stands answers.
- A reported credential's invalidation and the cancellation of its report are decided
  under a lock on the credential's row, so a report cancelled at the window's end is
  never invalidated after all; the invalidation also holds the account's row, which a
  second step's enrolment holds too, so the recovery codes and the password's change
  are judged on the factors the account holds.
- Linking a provider's identity is judged again on the account's row under a lock, so
  two links of one provider at once leave the account one identity of it and the second
  is refused `auth.factor.rejected`.
- The suspension a provider's withdrawal makes is decided on the account's row under a
  lock, so a deletion begun or a suspension made at the same moment is the one it
  follows and is never written over.
- A credential's removal, an unlink and a provider's withdrawal decide what the account
  keeps on its credentials read under a lock on each row, so two at once never leave
  the account with no way in, nor reaching less without the notified window.
- A restriction granted on a privacy request, a deletion window a fulfilled erasure
  begins and a takedown are decided on the account's row under a lock, so two takedowns
  at once take the account down once and a restriction never loses a takedown.
- An account's suspension, reactivation, deactivation, deletion, the lift of its
  restriction, the cancellation of its deletion and its reinstatement at recovery are
  decided again on the account's row under a lock, so two transitions at once end as
  they would one after the other and a link never stands up an account an administrator
  suspended meanwhile.
- A registration's age answer and its verification codes are decided on the session's
  row under a lock, so wrong codes presented at once are all counted towards
  `code.verification.attempts` and a refused date of birth is never lost to an answer
  given at the same moment.
- A sign-in or a step-up is completed under a lock on its challenge's row, from before
  the session is issued or raised until the challenge is removed, so one challenge
  completed twice at once issues one session and the second is refused
  `auth.factor.rejected`.
- Recovery approvals are counted against `recovery.ratelimit.account` and
  `recovery.ratelimit.approver` under one hold on the approvals, so approvals given at
  once never pass a day limit that approvals given one after another would reach.
- An enrolment session is held under a lock on its link from the start of the
  transaction that completes it and ended in that transaction, so two completions under
  one session at once write one credential and the second is refused
  `auth.enrolment.tokeninvalid`.
- A recovery link and an enrolment link are spent under a lock on the link's row, so
  one link completed or opened twice at once sets one password or opens one enrolment
  session, and the second is refused as a spent link.
- A credential a provider's event held is restored at a sign-in by another factor only
  where its row, read under a lock, is still held, so a credential invalidated meanwhile
  stays invalidated.
- A failed sign-in on a trusted browser is counted under a lock on the browser's row, so
  failures made at once are all counted and as many as
  `factor.trusteddevice.failurelimit` revoke its trust; a sign-in on it is judged again
  under the same lock.
- The silent rehash of a password onto raised parameters writes only where the row
  still holds the hash it verified against, so a sign-in with the old password never
  writes it back over a password set meanwhile.
- A security key's counter is judged under a lock on the credential's row, so two
  assertions reporting one counter at once are accepted once and refused
  `auth.webauthn.countermismatch` once, and the stored counter never moves backwards.
- A code generator's code is judged under a lock on the credential's row, so one code
  presented twice at once is accepted once and refused `auth.code.replayed` once.
- A recovery code is spent under a lock on its set's row, so one code presented twice at
  once is spent once and the second presentation is refused `auth.code.invalid`.
- A code try is decided under a lock on the code's row, so wrong codes presented at once
  are counted as the same number presented one after another, and the right code
  presented twice at once answers once. Each wrong try up to the cap is refused
  `auth.code.invalid`, the one reaching it ending the code, and whatever follows is
  refused `auth.code.expired`. The `emailCode` sign-in code and the code a sign-in link
  shows are authentication codes: capped by the new key `code.signin.attempts` (5,
  ceiling 10), the first living the new key `code.signin.lifetime` (10 minutes, ceiling
  30) and sent by mail alone as the new message kind `sign-in-code`, the second living
  as long as its link.
- The code and link that verify an address or a number being registered, added or
  replaced go out as the new message kind `verification-link`, whose templates name
  `{code}` and `{link}`; `verification-code` is the new-device check's code alone. A
  deployment that registers its own `IMessageTemplates` words the new kind too.
- The notices of a loss report, and of a removal that would lower the account's
  reachable assurance, go out as the new message kind `credential-suspended`, whose
  templates name `{link}`, the link that cancels the suspension.
- A fulfilled erasure request that starts an account's grace window tells the
  security-notice set with the new message kind `oob-deletion-notice`, which carries no
  cancel link; one fulfilled against a window already running sends nothing.
- A registration is held to the progressive delay as a sign-in is: a refused code is
  counted against the session's source and the identifier, and while the delay stands a
  code or a further ask for a code is refused `auth.throttled` with `retryAt`.
- A refused code of the new-device check is recorded as `auth.authentication.failed`
  with details `{"verification":"device"}` and no factor; a pressed link token that
  opens nothing is recorded against no account under the link factor the request
  named and counted against its source, so `IAuthentication.LandAsync` takes that
  factor and `/auth/factor` refuses a `linkToken` under any other factor
  `api.request.malformed` naming `factor`. A throttled provider return carries
  `retryAt` beside `error`.
- A sign-in link or email code sent to an address the account has removed since, and a
  sign-in opened with such an address, no longer signs in: the factor is refused
  `auth.factor.rejected`, recorded and counted. A held address that does not parse is
  judged by a domain lock as a domain that does not read, so every lock refuses it.
- The `phoneCode` second step is sent: `POST /auth/factor` or `/auth/step-up` naming
  `phoneCode` with no `value` answers 202 and texts a code as `secondstep-code` under the
  purpose `secondfactor`, living `code.signin.lifetime` and capped by
  `code.signin.attempts`, which answers only the sign-in or step-up it was asked for.
  The carrier's SIM-change or porting signal is now asked before that code, before a
  recovery link by text and for the combinations a step-up offers: on `risk` nothing is
  texted, the step-up withholds the text factors, and `POST /auth/link` and
  `/recovery/begin` answer 202 as for any number instead of refusing.
- The credential events are written in the transaction that makes them true, so an
  event that cannot be written fails the operation and nothing of it stands: a report
  or removal suspends nothing, and an enrolment enrols nothing. A password set on an
  existing account, in a session, through an enrolment session or by recovery, raises
  `CredentialEnrolled` with the kind `password` and no `Credential`, which is now
  nullable. `CredentialSuspended` names who reported the loss or asked for the
  removal as its `Actor`; `CredentialRestored` names the session's `Actor` and
  `Effective`, and nobody when cancelled from the link.
- `IAssuranceProvider.AttainedAsync` replaces `LevelAsync` and reports an
  `AttainedAssurance`: the level, whether it was phishing-resistant, when it was
  attained and the most the account can reach. A step-up gate judged from a host's
  report is met only where all four meet what the acting person's policy says the gate
  costs, and is otherwise refused `auth.stepup.required` with the gate it asks for.
- `concurrent-sessions-implausible` now compares a place whose country is known: two
  sessions whose countries differ raise it whatever their cities, and the distance is
  measured only where both places name a city.
- A value the library reads from text under a rule, left unset (such as its `default`),
  throws `InvalidOperationException` where its text is read, so no such value reaches a
  row.
- Under the mount, a path no endpoint serves and a method a path does not take answer
  404 `authz.resource.notfound` in the error envelope, and a fault answers 500
  `system.fault` with the correlation identifier and nothing of what was thrown; the
  log keeps under that identifier the full type name and stack frames of the fault and
  of each fault beneath it, and never a message, as it does for a fault a background job
  or a restore test step throws. The host's routes outside the mount answer as the host
  has them answer.
- An authorization request refused where the refusal cannot go back to a client is
  answered to the browser in the error envelope rather than as the provider's text:
  400 `api.request.malformed` with the protocol's code in `details.error`, or 500
  `system.fault`.
- Every error the OpenID Connect provider answers carries the protocol's `error` code
  alone: no `error_description` or `error_uri` in the body, the `WWW-Authenticate`
  header or the address a client is sent back to.
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
  The list is the 100,000 most prevalent hashes of Pwned Passwords, drawn from Have I
  Been Pwned over the range API and dated on its first line. Every range request names
  the library's package and its version in its user agent, as the provider asks of its
  callers; the version carries no commit identifier.
  A fall back from the configured corpus to the offline one is raised as `degradation`
  under the scope `password.blocklist.fallback`, naming both corpora. Where the alert
  cannot be raised, where neither corpus can answer, or where the corpus is older than
  the deployment admits, the password is refused rather than accepted unscreened. The
  `dictionary` source reads two word lists the package carries, the 12dicts 3esl English
  list and an Arabic transliteration list with its Arabizi forms, and nothing from the
  deployment's files; a host adds words to them by declaring `DictionaryWords`, and a
  refusal never names the word matched.
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
  reads stops the deployment too. Both are judged against the Public Suffix List the
  package carries, its ICANN and private sections alike: an identifier that is a public
  suffix is refused, and `shop.com` and `shop.co.uk` count as one name.
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
  on its own, or signs out everywhere; a session of another account is answered 404
  `authz.resource.notfound`, as one nobody holds. An administrator ends one account's
  sessions, and the emergency operation ends every session in the deployment.
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
  fingerprint keys, the maintenance credential, the host's
  declaration and the `ApplicationKind` the pipeline is mounted in. The maintenance
  credential is the database connection of a login holding the maintenance role's
  rights, read from the secrets manager; a deployment that supplies none does not start,
  with `model.startup.secretunavailable` naming `maintenanceCredential`. What the host
  declares about its own domain is built and checked here, at startup. The endpoints
  mount with `MapIdentityEndpoints` and `MapIdentityWellKnown` on `IdentityEndpoints`,
  and the two profiles with `UseBrowserProfile` and `UseMachineProfile` on
  `PipelineProfiles`. Only the namespaces, the package identifiers and `AddJanus` carry
  the product's name.
- The gate explains itself: an explanation names the grant that decided, the container
  it was inherited from, and the principal it was decided for, or states that no grant
  matched. An explanation can be asked with the host's own rows, and on a type a
  derivation reaches it names the grant the fact produced: no identifier, the derived
  kind, the role the derivation confers, and the container it was inherited from, the
  nearest where several admit the record. The identifier an explained grant carries is
  optional for that reason: a derived grant is a fact being true and no row holds it. A
  page of capabilities with the host's rows costs one query over those rows however many
  permissions it asks for: the stored grants and every derivation reaching the type are
  evaluated in that one query, a deny defeating a derived grant there, and what the role
  each derivation confers allows is read from the model, so a page that offers three
  actions costs what a page offering one costs. A single check with the host's rows is
  one query over them as well, and neither reads a grant through the library's own
  connection.
- A refusal on one record answers as a record that does not exist unless the type says
  otherwise, and a type says so in one place for every record of it. A type that
  conceals has no self-service explanation, because saying that no grant matched says
  that the record is there. The browser profile answers such a refusal as `404
  authz.resource.notfound` carrying the identifier it was recorded under, whatever the
  endpoint wrote after it, and the answer is the same whether or not the record exists.
- The refusal of a record the library holds no row for runs the same statements as the
  refusal of a registered one, the reading of the host's rows included, so how long it
  takes says nothing about whether the record exists.
- Every refusal carries a correlation identifier, whatever the request was made under,
  background work included, which is the audit row it was recorded as. A refusal of work
  done under neither identity is recorded with neither named, which is the recorded fact
  rather than an omission, and the database refuses any other kind of event that names
  neither. A caller holding `audit:read` in the administrative organization resolves the
  identifier to the permission and the principal, whichever organization the refusal was
  recorded in; it says nothing about whether the record exists. A permission that names
  no record is refused as a permission the caller does not hold, with nothing concealed.
- A check, a filter, a fragment, a capability page or an explanation naming a resource
  type or a permission the model does not declare raises at the request, before the
  caller's restriction is read or anything is recorded, so the calling code's fault is
  the same whoever asks. A capability page asked for such a permission raises rather
  than leaving it out, whatever a stored role still allows.
- A refusal is recorded with the grant that decided it, where one did, so its correlation
  identifier resolves to what the gate explained at the time: a deny grant is named with
  the container it sat on, rather than the refusal reading as one no grant matched.
- A refusal is recorded outside any transaction the caller holds open and committed at
  once, so a rollback of the caller's work leaves it standing: it still resolves by its
  correlation identifier and counts toward `alerting.denials.threshold`.
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
  type with no purpose, a purpose whose basis needs an assessment and names none, a
  derivation from a relationship that was never declared, and a type named
  `organization`, which the library reserves for the whole organization
  (`model.type.reserved`), each stop the deployment with their own code.
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
  `DocumentVersion` and `DocumentTranslation`, a send's correlation reference
  (`SendReference`, and the reference of `SmsDeliveryReport`), and the secret parameters
  of the service contracts. The marker states the rule for the host as it does for the library's own
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
- The identifiers of `Janus.Core` that a route carries implement `IParsable<T>`, and
  every endpoint binds them through it, so a route naming the max UUID as a subject is
  refused 400 `api.request.malformed` before any operation runs.
- `AccountState`, `SuspensionOrigin`, `DeletionOrigin`, `TakedownTrigger`,
  `ErasureStatus` and `ErasureReason` in `Janus.Core`: the state an account is in, why
  it entered the one it is in, and how far an erasure's host-side work has got.
- `ISecretSource`, `KeyEncryptionKeys` and `FingerprintKeys` in `Janus.Core`: the host
  supplies the key-encryption key, the fingerprint key and the maintenance credential,
  and the library ships no secrets-manager client and no default.
  `ISecretSource.ReadFingerprintKeysAsync` and `AddJanus` take a `FingerprintKeys`: the
  current version and every version still held.
- `IKeyRing` in `Janus.Core`: the one key ring the secrets read at startup are held in,
  each in a pinned array of its own, and lent for the length of one use. A read before
  the ring is filled, or after it is cleared when the application stops (after the
  worker and the web server), is a fault. The ring's reading is the first hosted service
  the library registers.
- `IUnitOfWork` in `Janus.Core`: an operation runs in one transaction and commits once,
  and hand-written SQL takes its connection from the one accessor, so a query written by
  hand runs on the same connection and inside the same transaction as the rest of the
  operation and can never miss a write the operation has already made. An operation that
  fails part way through leaves nothing written. `BeginAsync` and `CommitAsync` return a
  `Result`: an operation that returns a result returns their failure, and background
  work, which returns none, throws it as a fault naming its code.
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
- Two invitations acknowledged together for one account are decided one after the
  other, under a lock on the account's row, so they never leave more memberships than
  the setting allows; the database also refuses a second current membership of one
  organization for an account (`ux_memberships_current`).
- A route under `/admin/organizations/{id}` whose `{id}` names no organization the
  deployment holds answers `404` `identity.organization.notfound` and writes nothing,
  whichever organization its permission is asked in; issuing an invitation into an
  organization whose deletion was requested answers `authz.denied`.
- A change of an organization's policy or of its domains whose reason is absent or
  blank is refused `422` `config.change.reasonrequired` naming the key
  `policy.<organization>`, at the endpoint before any permission is asked and in the
  service alike; a policy replacement naming `emailDomains` is refused `400` naming it
  before any permission is asked.
- Verifying a domain the organization does not list, never listed or removed, answers
  `404` `identity.domain.notfound`.
- A deployment that registers no `IDnsResolver` lists no domain: adding one is refused
  `422` `config.value.notallowed` with `details.field` `emailDomains` and
  `details.requires` `dnsResolver`, and a deployment whose stored lock lists a domain
  does not start without a resolver (`model.startup.declarationmissing`, `details.key`
  `dnsResolver`).
- An invitation into an organization whose mail is integrated that names no personal
  `email`, no `corporateEmail`, or one address as both is refused `422`
  `identity.invitation.addressrequired`, naming the member; `identity.identifier.invalid`
  stays for an address that does not read.
- An invitation binds the phone it names without reading `registration.phone`, which
  takes `required` or `optional` only, so no deployment refuses a phone as one it does
  not collect.
- An invitation naming a role is also the `grant:manage` step-up action, judged after
  `invitation:issue`.
- An invitation naming a role the deployment does not hold is refused `422`
  `authz.grant.unresolved` naming `roles`, in place of `400` `api.request.malformed`.
- An invitation naming a legal document the deployment never published is refused `422`
  `api.request.invalid` naming `documents`; a blank name stays `api.request.malformed`.
- An invitation asserting a corporate address a member holds, or one a standing
  invitation that has not expired reserves, is refused `409` `identity.mailbox.taken`
  naming `corporateEmail`.
- Revoking an invitation the organization did not issue, or none, answers `404`
  `identity.invitation.notfound`.
- The membership step shows an inviter who shows no display name by their primary
  email, and by nothing only where neither reads.
- Acknowledging an invitation tells the membership limit and the email maximum before
  the organization's credential policy, so nobody is sent to enrol for a membership
  they cannot take.
- Acknowledging an invitation judges the organization's domain lock as it then stands
  on the address the member will sign in with, and refuses one outside it with
  `identity.identifier.domainnotallowed`.
- An acknowledgement held at enrolment names the unmet requirement as
  `policyRequirement` `{ field, value }`, with no deadline, in place of a flat `field`
  and `value`.
- An invitation whose inviter no longer manages the organization's memberships, or may
  no longer grant a role it names, is answered as expired at acknowledgement and grants
  nothing.
- A role the invitation grants is written beside an expiring grant of the same role,
  and is skipped only where the account holds it permanently.
- Taking the corporate address on at an acknowledgement publishes `IdentifierAdded`,
  and retiring it at the end of the membership publishes `IdentifierRemoved`, each in
  the transaction that makes the change.
- Ending a membership is the `membership:end` step-up action, and `EndMembershipAsync`
  takes the session it is judged on. An account holding no current membership of the
  organization, a second end included, answers `404` `identity.membership.notfound`
  before any step-up is asked.
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
  covered. Every grant the account holds is revoked in the same transaction, by the nil
  subject at the erasure's instant with the reason `IDN-LIFE-014`, and every grant row
  is kept. A transaction that does not commit leaves no row and erases nothing; there is
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
  refused. A grant in the administrative organization confers only while its holder
  holds a current membership of it: ending that membership stops what the grant
  confers and removes no grant.
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
  preferred second step, is refused with `authz.restricted`, and so is acknowledging an
  invitation. A restricted account signs in, as an active one does, with its factors,
  its provider, its sign-in link and the mail server's sign-on, and recovers its
  password; the enrolment an approved recovery opens is not refused. The restriction
  ends every session of the account in the transaction that makes it, and a restricted
  account's sign-in is offered no trusted browser and records none. Its mailbox stays
  owed enabled and its app passwords keep working.
- Registration is served end to end. A browser that reaches the library is given a
  pre-authentication session, and the registration it starts is bound to that session
  and reachable from no other browser: the age screen, the email and phone steps, the
  confirm screen, the security step and the terms step, each refusing to run before the
  one before it has finished with 409 `identity.registration.incomplete`, a confirmation
  while a staged identifier is unverified included. An address or a number that already
  belongs to somebody else is answered exactly as a fresh one is, and its holder is told
  once that somebody tried. An address held out of reach for its owner's undo is
  answered the same, with nothing sent and nobody told, is not vouched for by a
  provider, is refused as a held one where an invitation binds it, and one taken or
  reserved since it was staged ends the registration at the terms step with no account
  created. A registration that is abandoned leaves nothing behind. A browser that
  already holds a session and asks to register is refused with 409
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
  to the deployment's maximum, made primary, set as the backup destination (an
  unverified one is refused either with 409 `identity.identifier.unverified` and nothing
  changes), removed with an undo the remaining addresses are sent, and, where only one
  of a kind is allowed, replaced in one operation. A removed identifier stays out of
  reach of every other account until its undo window closes. Once a replacement applies
  every session of the account but the one it completed under ends, the one that staged
  it included, and a replacement the displaced address's link completes ends them all.
  The session that removes an identifier or completes the verification of one is given a
  new secret, the one before it answering nothing. The session list marks the one asking
  and says no more about where each was used than the city. A credential given a label
  or renamed is audited as `auth.credential.labelled`.
- `PUT /account/secondstep/preferred` takes the member `method`, the identifier of a
  second factor enrolled on the account; a method the account has not enrolled, or one
  that is no active second step, is 422 `api.request.invalid` naming `method` and leaves
  the preference as it was.
- A profile field the deployment has switched off is neither accepted from a request nor
  carried in an answer, and the date of birth is never the person's to change. A
  preference key the host never declared is refused and never returned. A username, once
  chosen, is held against every other account for the cooling-off period after it is
  given up, and after erasure for the same period. A username with a word that mixes
  scripts is refused with `identity.identifier.mixedscript`, as a display name is.
- The library serves `/.well-known/change-password`, `/.well-known/passkey-endpoints`
  and `/.well-known/webauthn` at the site root, mounted with `MapIdentityWellKnown`. The
  addresses of the frontend's password and passkey pages behind the first two are a
  declaration with no default: a deployment that registers none, or leaves one of them
  or a sign-in address empty or blank, does not start, naming the declaration or the
  field it left out, so both documents always answer. The third is the deployment's own
  related-origin allowlist.
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
  asked for the second step again, a remembered one faces the check again. Another
  account's browser is answered 404 `authz.resource.notfound`, as one nobody holds.
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
- A consent is a record for each grant and an objection a record for each objection:
  `identity.consents` and `identity.objections` are keyed on `id`, a partial unique index
  (`ux_consents_live`, `ux_objections_standing`) holds at most one live record a
  subject and purpose, and `GET /privacy/consents` and `GET /privacy/objections` answer
  every record, the withdrawn and superseded ones as they were. A grant after a
  withdrawal or a supersession adds a record and changes none. A grant over a live
  record the purpose admits (given against the document the purpose names, written
  where it requires written), and an objection while one stands, are answered as
  success and record and raise nothing, two at once included. A grant over a live record
  the purpose no longer admits stamps it superseded and adds the new one in one
  transaction, `ConsentChanged` `superseded` then `granted`, and is recorded as
  `reconsent` where it was named `dashboard`. The gate reads the live record, or the
  latest where none is live. The migration `KeepARecordForEachGrant` gives each record
  held an `id` and deletes none; reverting it is refused where a subject holds a second
  record of one purpose.
- The link that asks the address a replace displaces to confirm it is sent under the
  `verification` purpose, as the new address's code is, so no `notification`
  restriction counts or refuses it.
- A registration stream whose wait begins while the database channel that wakes it is
  no longer listened on raises `degradation` with `details.component`
  `registration-channel`, once per deduplication window, and the channel is opened
  again; the stream goes on reading the state back on its interval meanwhile.
- A change of an alert destination list is written only while the list whose
  destinations it notified is still in force. Where another change of the list
  committed after the notice went out, the change writes nothing, raises no
  `alert-destination-changed` and is refused with the new code
  `config.change.superseded` (409), so no destination is replaced without having been
  told.
- `IGovernedSend` in `Janus.Core` is the one way an area undertakes a send:
  `UndertakeAsync` takes an `OutboundMessage` (the destination, the message, the purpose,
  the source and the language, with the subject and the values) and answers the
  `SendReference` the admitted message is carried under, or the refusal. It answers the
  admission and never the delivery. The send is judged against the named restrictions
  and the gateway floor inside the caller's unit of work, with the counter of each of
  its keys held to the end of that transaction, so of several sends judged at once
  while a bucket has room for one, one is admitted and the others are refused with
  `retryAt`, and a credit is spent once. A send counts from its admission; its count
  and the credit it spent are given back where it fails for good: its attempts are
  spent, a delivery report says it failed, or the restrictions refuse its retry. A
  refused send writes neither a count nor an outbox row. The caller begins the unit of
  work: a send undertaken outside one is a fault.
- `IUnitOfWork.AfterCommit` registers work to run once the outermost transaction has
  committed; a rollback discards it. An admitted message has one attempt registered
  this way, so no transport is called while a transaction is open and an operation
  that rolls back sends nothing. The message of an ask of a sign-in link, an email code
  or a recovery, and the notice to an address no account holds, has no attempt in the
  request: the ask is answered first and the outbox publisher carries it.
- `INotificationHandler.SendAsync` now carries one message the library has already
  admitted and written to its outbox, and answers whether a transport took it. Its
  `SendRequest` is the admitted message: the destination, the message, the language,
  the subject, the values and the `SendReference` it is carried under, with none of the
  restrictions' inputs. A deployment that registers its own handler is governed as the
  shipped one is. `SendReference.TryParse` reads a reference back from text.
- A send in every declared language is judged once. By mail it is one message, composed
  from each language's template in the order of `notification.languages` with the
  subject lines joined, and counts once. By text message it is one message for each
  language, each under a reference of its own, admitted only where every bucket has
  room for all of them, and each counts.
- Every admitted message is claimed before its handler is called, by the attempt that
  follows the commit and by each pass of the publisher, in any number of processes, so
  each is carried once and each outcome recorded once. The claim stands for
  `outbox.claim.timeout` (new, `PT2M`, floor `PT30S`, ceiling `PT10M`); an attempt still
  running then is abandoned as a failed attempt, and the next pass may take the row. A
  message carried again is judged by the restrictions as they stand then, with its own
  count set aside, and counts at that instant.
- An invitation's link, a recovery's link, a sign-in link or code, a second-step code,
  a new-device code and every notice an operation owes are undertaken in the
  transaction of that operation. An invitation whose link the restrictions refuse is
  not issued and reserves nothing.
- No event is emitted for a message: every message is carried from the library's send
  outbox to the notification handler, and an alert travels on its `AlertRaised` row.
  The event type `NotificationRequested` is retired with it.
- An erased wrapped key is 32 zero bytes wherever one is held, with no marker byte in
  them, and every unwrap refuses that value before it is tried. An erasure overwrites
  with it the key of every message admitted for the subject and not yet carried, in the
  erasure's own transaction; the publisher and the attempt that follows a commit remove
  such a row without carrying it, and the count the send held is released. A released
  mailbox reservation's key is overwritten with the same value. An outbox row keeps the
  hash of the reference it is counted under beside its encrypted content.
- A raised alert is claimed before the router carries it, by one conditional update
  committed on its own, and leaves the table under that claim, so passes of the alert
  channels in several processes carry each raised condition once. The claim stands for
  `outbox.claim.timeout`; a condition the router refused gives its claim up and is
  taken by the next pass.
- A mailbox push is claimed before the mail server is called, by one conditional update
  committed on its own, and its attempt and its outcome are each written under that
  claim, so passes of the mailbox publisher in several processes make each push once
  at a time and count each attempt once. The claim stands for `outbox.claim.timeout`,
  and a push still with the server then is abandoned as a failed attempt. A pass
  decides on the mailbox as its row stands once claimed and writes the push alone, so
  it never writes back a holder or a state owed it read earlier.
- The view `identity.consented_resources`, written by the migration
  `AddConsentedResources`: a row for each registered record whose data subject holds a
  consent neither withdrawn nor superseded, with the purpose, the document and the kind
  of that consent. The application role reads it.
- `ConsentedResource` in `Janus.Core`, the row of `identity.consented_resources`, mapped
  by `MapAuthorizationTables` beside `AncestryEntry` and `EffectiveGrant`.
  `FilterSources<TResource>` takes its `IQueryable` as a third required source, so a
  host passes `context.Set<ConsentedResource>()` beside the other two. For a permission
  bound to a consent-based purpose, `FilterAsync` and `FragmentAsync` admit only the
  records whose data subject holds a live consent for that purpose, recorded against
  the document the purpose now names and written where the purpose requires written
  consent; the fragment carries the purpose, the document and the kind as parameters. A
  check and a capability page refuse a live consent recorded against another document
  with `privacy.consent.superseded`, as the lists leave its record out.
- Startup refuses a relationship source whose context maps the ancestry and the
  effective grants and not the consented resources, with
  `model.startup.declarationinvalid` naming the source and `context`.
- At start, before the server serves, every live consent of a consent-based purpose
  recorded against another document than the one the declaration now names for that
  purpose is stamped superseded, with `ConsentChanged` `superseded` raised for each in
  the same transaction. The stamp is one conditional statement, so of several processes
  starting each consent is stamped and announced once.
- The security step of a registration enrols a passkey, a security key or an
  authenticator app before the account exists: `POST /auth/webauthn/register/begin`
  and `complete`, and `POST /account/factors/totp/begin` and `confirm`, accept the
  registration session in place of an account session while its step is `security` or
  `terms`, and `CredentialAuthority.Of(RegistrationSessionId)` is the authority
  `ICredentials` takes for it. The ceremony runs under the session's provisional
  handle and the staged email. The open ceremony and the unconfirmed secret are held
  in the session's encrypted document and nowhere else, one of each, spent or replaced
  under the session's lock; the terms step writes only a credential that was created or
  confirmed, and an abandoned or expired session leaves nothing of either.
- A registration's verification codes are issued and answered through the
  verification-code record every channel verification code uses, held against the
  session and the staged identifier, so a wrong try is counted and the right code
  spent under that record's lock. A code presented after the attempt cap, and one
  presented for an identifier with no code outstanding, is now answered
  `auth.code.expired` where it was answered `auth.code.invalid`.
- An address or number another account holds, or one reserved for an undo, is given at
  registration a verification-code record with the lifetime and attempt cap of a sent
  code and no code that any presentation matches: a code presented for it is answered
  `auth.code.invalid` up to `code.verification.attempts` tries and `auth.code.expired`
  after them and after `code.verification.lifetime`, as a wrong code for a fresh value
  is. The ask, and a resend, is counted against the sending restrictions as its
  message would be and refused by them alike.
- The code that verifies an identifier added to, or replaced on, an account is issued
  and answered through the same verification-code record, held against the pending
  verification. A code presented after the attempt cap is now answered
  `auth.code.expired` where it was answered `auth.code.invalid`. A code outstanding
  when this version is deployed no longer verifies: a resend replaces it.
- `GET /register/events` answers a browser that carries no registration session 404
  `authz.resource.notfound`, with the body every refusal carries, where it answered a
  404 with no body.
- An email or a phone offered to a registration before its age step is answered is
  refused 409 `identity.registration.incomplete`, as every step asked for before the
  one it follows is, where it was refused 422 `identity.affirmation.required`, which is
  the terms step's alone.
- `POST /account/recoverycodes` answers only the holder of a session: a browser holding
  an enrolment session and no other is refused 401 `auth.session.expired`, where it
  was answered as the account, or 422 `auth.enrolment.tokeninvalid` once the enrolment
  had ended.
- `POST /privacy/requests` refuses a body whose `type` is `erasure` 400
  `api.request.malformed` naming `type`, where it was refused 403 `authz.denied`.
- Every endpoint carries, as endpoint metadata, each answer it produces: the status and,
  where it writes a body, the body's type and content type, beside the codes it declares.
- `derivation.materialised.driftcheck` is a key of the configuration: it is validated at
  startup, read and set through `GET` and `PUT /admin/config/{key}` and the command line, and
  listed with the others, where the drift check read it and nothing else knew it.
