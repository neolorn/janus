# 21 — People without an account

The person record: a data subject the host keeps data about who holds no account; how a
host collects it in its own screens, how the person reaches it by a code, how it is
attached to an account, and what staff do with it.

**Prerequisite:** `01-identity` (the account, IDN-ACCT-002, the takedown of
IDN-LIFE-003), `04-privacy` (consent, the rights, the subject key),
`20-registration-and-account` (the age screen, the terms step).

**Scope.** This document covers *a person the host keeps data about in their own right
who holds no account*: a guest buyer, an applicant, the sender of a form, a contact
staff enter for someone who called. It states the library's part: the record, the
session that verifies a contact at collection, the call that writes the record, the code
that opens the person's data to them, the link to an account, and what staff do with
records. The screens a host collects in are the host's own (`18` FE-REC-001). The staff
search over people is not here (plan, round S). Endpoints, shapes and status codes are
`09` section 7a and section 8a; names, defaults and codes are `10`.

---

## 1. The person record

**REC-REC-001** — A person whose personal data a host keeps in their own right, and who
holds no account, SHALL be a **person record**: a data subject of its own, with its own
subject identifier, its own data key and its own consents, and no way to sign in.

**Values (D-221).** A record's subject identifier is issued as an account's is
(IDN-ACCT-002): opaque, derived from no personal attribute, never reused, and drawn from
the one space subject identifiers share, so no record and no account ever bear the same
one. Its data key is a row of the subject-key table as an account's is
(PRIV-RIGHT-005a): a host encrypts its own fields about the person under the record's
subject through `IPersonalFields`, and registers its records naming the record's subject
as their data subject (`03` AUTHZ-INHERIT-002), exactly as it does for an account. A
record holds:

- its **organization**: the organization the host keeps the data in, named at its
  writing and never changed;
- its **origin** (`10` section 5.51): `collection` where the person gave the data in a
  host's own screen (REC-COL-002), `staff` where a member of staff entered it for them
  (REC-COL-003);
- its **state** (REC-REC-002) and when it was created;
- its **language**: one of `notification.languages`, the language every message to it is
  sent in;
- a **name**, optional: a display name under REG-PROF-001's rule (PRECIS Nickname, 1 to
  64 bytes, IDN-ACCT-005 per word), a personal field under the record's key;
- its **contacts**: at most one email and one phone, each canonicalised as an account's
  identifier is (IDN-ACCT-004), its value under the record's key, a keyed fingerprint
  beside it (PRIV-RIGHT-005c), and the instant it was last verified at a collection, or
  none where it never was;
- its **references**: what the person holds of a collection (an order number, say), each
  held only as a keyed fingerprint (REC-COL-002);
- the **age answer**, where one was given (REC-AGE-001): the affirmation, or the age
  group, with the instant, and the date of birth under the record's key only where
  `profile.dateofbirth` is on;
- each **presentation** of the privacy notice and each **acceptance** of the terms its
  collections recorded, with the version and the instant (PRIV-CONS-008a);
- the **account** it is attached to and the instant, where it is (REC-ATT-001).

A record holds no credential, no session of the library's sign-in, no grant, no
membership and no group membership: a grant, a membership or a group member naming a
record's subject is refused as one naming a subject no account bears. Its consents and
objections are recorded against its subject (PRIV-CONS-001, PRIV-RIGHT-001a), and
`ConsentChanged` and `ObjectionChanged` name it. Its rights are exercised by a code
(REC-ACC-001), through the account it is attached to (REC-ATT-002), or out of band
(PRIV-RIGHT-001), whichever reaches it.

A record is for a person whose data the host keeps **in their own right**. A third party
a person names inside their own record (a delivery recipient, a referee) stays under
that person's key and is no record (IDN-LIFE-002a, D-131).

*Source: D-221*

A separate record, not an account with its parts missing: no rule of the account is
weakened for it, and no account is ever merged into another to take a record in
(IDN-ACCT-003), since a record is attached and never moved (REC-ATT-001).

**Acceptance criteria**
1. A record's subject identifier is never an account's, and an erased record's is never
   issued again.
2. A host's field encrypted under a record's subject decrypts under it and under no
   account's, and is unreadable once the record is erased.
3. A grant, a membership and a group member naming a record's subject are each refused
   as one naming a subject no account bears.
4. A record holds at most one email and one phone; a dump holds no form of a contact's
   value or of a reference.

---

**REC-REC-002** — A person record SHALL be in exactly one state at any time.

| State | Meaning | Reached by a code (REC-ACC-001) |
|---|---|---|
| `active` | Normal | Yes |
| `restricted` | Processing restricted at the person's request (PRIV-RIGHT-004) | Yes; its reading actions and its rights, and no change of its own fields |
| `deleting` | Deletion requested, grace window running | Only where its own deletion began it (`self`), to read it and to cancel |
| `deleted` | Personal data key-destroyed, the row kept (IDN-PRIN-003) | No |

**Values (D-221).** `deletingBy` is `self` · `takedown` · `oob-request`, as an account's
(`10` section 5.12b). There is no `suspended` state, since a record signs nothing in: a
takedown takes a record straight into `deleting` (REC-TKD-001). A record holds what it
left as an account does, with `restrictionHeld`, `deletionHeld` and `deletionHeldSince`
(`10` section 5.12b) and no `suspensionHeld`, and a cancellation or a reversal restores
it as an account's restores the account. A deletion begun by `self` or `oob-request`
runs `record.deletion.grace` and a takedown's `takedown.grace`; when the window closes
the sweep erases the record (REC-REC-003).

An operation the record's state does not admit is refused with
`identity.record.stateconflict` (409), `details.state` naming the state; a path naming a
subject no record bears, or one outside the caller's reach, with
`identity.record.notfound` (404).

*Source: D-221*

**Acceptance criteria**
1. Every record has a state; none is null.
2. A record's own deletion cancelled inside its window returns it `active`, or
   `restricted` where a restriction is held; a reversal of its takedown returns it to
   what it held at the trigger.
3. An operation a state does not admit is refused with `identity.record.stateconflict`
   naming the state.

---

**REC-REC-003** — A person record's erasure SHALL be the erasure an account's is: one
transaction that destroys its key, neutralises its fingerprints and records the erasure,
the row kept.

**Values (D-221).** The transaction overwrites the record's wrapped key with the erased
value and the wrapped keys of its outstanding outbox rows (PRIV-RIGHT-005a), neutralises
the fingerprint of each of its contacts and references (PRIV-RIGHT-005c), sets it
`deleted`, and writes the erasures row (IDN-LIFE-003b) with its reason
(`erasure-request`, `minor-takedown` or `retention`, `10` section 5.12a) and the
`ErasureRequested` outbox record naming the record's subject (IDN-LIFE-003a). The
off-host ledger takes its line as for an account (DR-016). Nothing of the record's row
is removed: its subject, organization, origin and instants remain, with nothing that
identifies the person.

*Source: D-221*

**Acceptance criteria**
1. After a record's erasure no personal field under its key decrypts, no fingerprint of
   its contacts or references matches, its row remains `deleted`, and `ErasureRequested`
   names its subject.
2. A crash during it leaves no erasure, or a whole one (IDN-LIFE-003b AC4).

---

## 2. Collection

**REC-COL-001** — A host that collects a person's data in its own screens SHALL do so
through a **collection session**, which asks the age question first where REC-AGE-001
requires it and verifies each contact by a code.

**Values (D-221).** `POST /records/collection` creates the session: an opaque
server-side record bound to the requesting browser through the pre-authentication cookie
(BFF-CSRF-005a), as a registration session is (REG-SESS-001), living
`records.collection.lifetime` from its creation, that **stages** the age answer and the
contacts and **reserves nothing**. Its body says whether the age question is asked
(`age`). Nothing is written outside the session store but the records of the abuse
controls (AUTH-ABUSE-001, AUTH-ABUSE-004), the verification-code records of its contacts
(AUTH-FACT-004) and the messages sent, as for a registration (REG-SESS-002 AC2). A
browser holding a live sign-in session is refused with `identity.record.signedin` (409):
a person who holds an account gives their data under it. An expired or abandoned session
is swept and leaves nothing behind.

- **The age.** Where the session asks it, `PUT /records/collection/age` takes the date
  of birth before any contact, and is answered as REG-PROF-002's screen is: with
  `registration.adultaffirmation` `required` the affirmation is derived and staged, and
  a date under eighteen ends the session with `identity.profile.underage` and locks it
  against retry, the end being that refusal's kept write (CONV-DESIGN-003); with it
  `off` the age group is staged. An age the session does not ask, or already holds, is
  refused with `identity.collection.incomplete`.
- **A contact.** `PUT /records/collection/email` and `PUT /records/collection/phone`
  stage the value, canonicalised (IDN-ACCT-004), and send a code to it
  (`verification-code`, under the `verification` purpose), as a registration's
  identifier is sent one (REG-SESS-003), save that no link is sent: the person types the
  code into the host's screen. A contact staged again replaces the one staged and sends
  a new code. Each is answered 202 in the same bytes whether or not an account or a
  record holds the value. A contact before the age the session asks is refused with
  `identity.collection.incomplete`.
- **The code.** `POST /records/collection/verify` verifies a staged contact by its code,
  held to `code.verification.lifetime` and `code.verification.attempts` (AUTH-FACT-004),
  every code first held to the delay of AUTH-ABUSE-001 and a wrong one counted against
  the source and the value.
- `GET /records/collection` answers the session's state, and
  `POST /records/collection/abandon` ends it.

*Source: D-221*

**Acceptance criteria**
1. With `age` asked, no contact is taken before the age is answered; on an adults-only
   host an under-age date ends the session, and the same session takes no further date
   and no contact.
2. A contact's code verifies only through the browser that began the session.
3. Staging a value an account holds, a value a record holds and a value no one holds are
   answered in identical bytes.
4. Begun from a browser holding a live sign-in session, nothing is staged and the
   request is refused with `identity.record.signedin`.
5. An expired or abandoned session leaves nothing behind but the records REG-SESS-002
   AC2 names.

---

**REC-COL-002** — The record SHALL be written by `IRecordCollection.CollectAsync`, in
the host's own unit of work, from the collection session that the browser of the request
being served holds.

**Values (D-221).** `IRecordCollection` (`Janus.Core`) is a seam, as `IResources` is
(`07` LIB-API-005): `CollectAsync` joins the host's unit of work, has no endpoint and
takes no access context, since the person holds none. What admits it is the collection
session bound to the browser of the request being served, read through the request's
pre-authentication session (BFF-CSRF-005a), as the library's own collection endpoints
read it; called outside a browser request it is a fault of the calling code. It takes a
`RecordCollection`:

- `organization`: the organization the host keeps the data in;
- `language`: one of `notification.languages`;
- `name`: optional, under REC-REC-001's rule;
- `noticeVersion`: required, the version of the privacy notice the screen presented
  (PRIV-CONS-008a);
- `termsVersion`: optional, the version of the terms (the document `terms`, `10` section
  5.52) the screen asked the person to accept, where the host has terms;
- `consents`: the consent-based purposes the person ticked, one control each
  (PRIV-CONS-002, PRIV-CONS-003);
- `contacts`: optional, at most one email and one phone the screen took and did not
  verify, none of a kind the session verified;
- `reference`: optional, 1 to 64 characters after trimming, what the person will hold of
  this collection (an order number, say).

It answers a `Result<CollectedRecord>`, `{ subject, created }`. Under the lock of each
verified value (CONV-DESIGN-003), it **reuses** the record of `organization` that is
`active`, attached to no account, and holds a contact the session verified as a contact
last verified at a collection within `records.proof.age`, and whose every contact either
equals the session's of its kind or is of a kind the session did not verify, the most
recently verified where several do; otherwise it **creates** a record, origin
`collection`. A reused record gains a contact of a kind it held none of. Each contact
the session verified has its instant set to now; a contact passed in `contacts` is kept
unverified on a record created, and on a reused one only where it holds none of its
kind. In the same unit of work it records the presentation of `noticeVersion` and the
acceptance of `termsVersion` with the instant, a consent for each purpose in `consents`
with the mechanism `collection` against the current version of the document governing it
and of the kind it requires (PRIV-CONS-001, PRIV-BASIS-003), raising `ConsentChanged`
`granted` for each (a purpose held live and admitted adds nothing, PRIV-CONS-001), the
session's age answer where the record holds none (REC-AGE-001), the name and language
where given, and the reference as a keyed fingerprint of its `NFKC_Casefold` form
(PRIV-RIGHT-005c), never in any other form. The session is spent with it. The record is
the acting and effective identity and the `subject` of its audit record,
`identity.record.collected`, filed under the organization.

A refusal writes nothing and keeps nothing, so `CollectAsync` is called inside the
host's unit of work:
- no collection session the browser holds, or one expired, ended or spent:
  `identity.collection.expired` (422);
- a staged contact unverified, an age the session asked unanswered, or no contact at
  all: `identity.collection.incomplete` (409);
- `noticeVersion` or `termsVersion` that is not the current version of its document
  (`privacy-notice`, `terms`): `privacy.consent.superseded` (422), `details.member`
  naming it; no version of a document it names published: `privacy.notice.unpublished`
  (409), `details.document` naming it (D-222);
- a purpose that does not rest on consent: `privacy.purpose.noconsent` (422);
- an organization the deployment does not hold, or whose deletion is requested, a
  language outside `notification.languages`, a contact in `contacts` of a kind the
  session verified or not a well-formed value of its kind, or a reference outside its
  bounds: `api.request.invalid` (422) naming the member;
- a name outside its rule: `identity.profile.invalid` (422).

*Source: D-221*

**Acceptance criteria**
1. A collection whose session verified a phone writes a record holding the phone as
   verified now, the notice and terms versions with the instant, and one consent per
   ticked purpose recorded `collection`; a second collection with the same phone in the
   same organization within `records.proof.age` reuses that record, and one after it, or
   with another email than the record holds, writes a new one.
2. A collection from a request whose browser holds no collection session is refused with
   `identity.collection.expired` and writes nothing.
3. A collection naming a notice version that a later publication superseded is refused
   with `privacy.consent.superseded` naming `noticeVersion`.
4. A reference given at a collection is matched later (REC-ACC-001) and held in no
   readable or reversible form.
5. Two collections with one phone, run at once on two connections in one organization,
   end with one record.

---

**REC-COL-003** — Staff SHALL be able to enter a person record for someone who gave
their details otherwise (by telephone, in person, on paper), under `record:create`.

**Values (D-221).** `IPersonRecords.EnterAsync` (`POST /admin/records`), an operation
under `record:create` in the organization it names, takes `organization`, `language`,
and `name`, `email` and `phone`, at least one of the three (none given is refused with
`api.request.invalid` naming `name`, D-222); `consents`, the purposes the person gave
consent for to the staff member, each recorded with the mechanism `administrator`;
`dateOfBirth`, the person's own answer where the staff member asked them the age
question (REC-AGE-001); and `reference`. It writes a record of origin `staff` whose
contacts are unverified and answers its `subject`. Its refusals keep nothing, so it may
be called inside the host's unit of work. The staff member is the acting identity of
`identity.record.entered`, the record its `subject`, filed under the organization.

A record staff entered is reached by no code (REC-ACC-001) and offered to no account
(REC-ATT-001), since no contact of it was proven by the person: its rights are exercised
out of band (PRIV-RIGHT-001), through REC-ADM-002.

*Source: D-221*

**Acceptance criteria**
1. A record entered by staff holds its contacts unverified, and no code reaches it.
2. A consent entered with it is recorded with the mechanism `administrator`.
3. An entry without `record:create` in the organization is refused with `authz.denied`.

---

## 3. The age question

**REC-AGE-001** — A person record that holds sensitive data SHALL carry the age answer
from its collection, asked first, before any contact.

**Values (D-221).** The question is REG-PROF-002's neutral age screen, asked by the
collection session that asks it (REC-COL-001), and recorded as at registration: the
affirmation with its instant where `registration.adultaffirmation` is `required`, the
age group (`10` section 5.22) where it is `off`; the date retained under the record's
key only where `profile.dateofbirth` is on.

- **Enforced at the sensitive record.** The registration of a record of a sensitive type
  naming a person record as its data subject is refused with 422 `api.request.invalid`
  naming `subject` where the person record holds no affirmation (or, where
  `registration.adultaffirmation` is `off`, no age group), or is `deleting` or `deleted`
  (`03` AUTHZ-INHERIT-002, IDN-LIFE-002a). A record attached to an account is admitted
  on its account's affirmation.
- **Other collections.** Where the host keeps no sensitive data under the record (a
  newsletter, a contact form), the host chooses per screen whether its session asks
  (`age`), and by default it does not (PRIV-PRIN-001).
- **Staff never answer for someone.** A record staff enter holds no age answer, save the
  answer the person gave when the staff member asked them during the conversation (an
  order with sensitive data taken by telephone), passed as `dateOfBirth` (REC-COL-003)
  and recorded as the person's answer with the staff member acting. Where
  `registration.adultaffirmation` is `required`, an answer under eighteen refuses the
  entry with `identity.profile.underage` and writes nothing, the service being for
  adults only (PRIV-MINOR-001, D-222).

A host that serves minors records the age group, and a minor's sensitive data rests on
the written-consent basis PRIV-MINOR-001 requires.

*Source: D-221; amends IDN-LIFE-002a and PRIV-MINOR-001*

**Acceptance criteria**
1. A sensitive record registered under a person record holding no affirmation is refused
   with `api.request.invalid` naming `subject`; the same registration under a record
   collected with the age answered is admitted.
2. On an adults-only host, an under-age answer at collection ends the session, and no
   contact is taken from it.
3. A record entered by staff without a date of birth holds no answer, and no sensitive
   record registers under it; one entered with the person's answer does.

---

## 4. The person's access by a code

**REC-ACC-001** — A person SHALL reach, by a code sent to an email or phone, the person
records that hold it as a contact they verified at a collection, through a **record
access session**, without an account and without contacting anyone.

**Values (D-221).**
- **The ask.** `POST /records/access` takes `kind` and `value` and is answered 202 in
  the same bytes, on the same path and with no wait for the handler, whatever is held
  (AUTH-ABUSE-003, API-CONV-005); every ask is first held to the delay of AUTH-ABUSE-001
  for the source and the value. Where a **reachable** record holds the value as a
  contact verified at a collection, a code is sent to it (`record-access-code`, an
  authentication code held to `code.signin.lifetime` and `code.signin.attempts`,
  AUTH-FACT-004, under the `signin` purpose); otherwise the address is told, once per
  `abuse.nonexistent.window`, that nothing can be shown for it by a code
  (`record-access-none`), the window spent only where the send is admitted, as
  AUTH-ABUSE-003 tells an address no account holds. A code to a phone is sent only where
  the phone signal (AUTH-FACT-002b) does not answer `risk`; on `risk` nothing is sent
  and the answer is the same. A browser holding a live sign-in session is refused with
  `identity.record.signedin`.
- **Reachable** is a record attached to no account, whose state is `active`,
  `restricted`, or `deleting` by `self`.
- **The session.** `POST /records/access/verify` takes the code in the browser that
  asked, and opens a record access session: a server-side record bound to that browser's
  pre-authentication session (BFF-CSRF-005a), living `records.access.lifetime` from its
  opening, never extended, holding the value's fingerprint and naming the records it
  reaches. A wrong code is counted against the source and the value (AUTH-ABUSE-001).
- **Its reach.** The reachable records holding the value as a contact last verified at a
  collection within `records.proof.age` before the session opened. A record whose last
  verification of the value is older is reached only once the person gives a reference
  it holds, at `POST /records/access/reference`, which adds every reachable record
  holding the value and that reference, whatever its age; a reference that adds none is
  counted against the source and the value as a wrong code is, and is answered as one
  that adds some, with the session's state. A record staff entered, one attached to an
  account, one taken down or out-of-band deleting, and one `deleted` are never reached.
- **Every action** re-reads, under the record's lock, whether the record is still in the
  session's reach; one that has left it, and one it never reached, are answered
  `identity.record.notfound` alike. A session past its lifetime, ended, or presented
  from another browser is answered `auth.session.expired` (401).
  `POST /records/access/end` ends it.
- Each record the session reaches, at its opening or by a reference, is recorded as
  `identity.record.accessed`, the record acting and its `subject`, filed under its
  organization.

*Source: D-221*

The code goes to the address, never to the person asking, so an enumerator learns
nothing from the answer. The age rule answers numbers given up and issued to someone
else: a code proves who holds the number now, not who held it when a record was
collected, so an older record asks for something only its collector holds.

**Acceptance criteria**
1. An ask for an address a reachable record holds and one for an address none holds are
   answered in identical bytes; the first sends a code, the second `record-access-none`,
   once per window.
2. A record whose phone was verified at a collection inside `records.proof.age` is
   reached by the code alone; one verified longer ago is reached only once its reference
   is given; a reference that matches nothing reaches nothing and is counted.
3. A record staff entered, one attached to an account and one taken down are never
   reached.
4. A session is refused from another browser, after `POST /records/access/end`, and once
   `records.access.lifetime` has passed since it opened.
5. With the phone signal answering `risk`, no code is sent to the phone and the ask is
   answered as any other.

---

**REC-ACC-002** — Within a record access session the person SHALL exercise every right
on each record it reaches, with no human approval save where PRIV-RIGHT-001 puts one.

**Values (D-221).** Under `/records/access/{subject}` (`09` section 7a):
- **Access.** The record's read: its organization (and the organization's name), origin,
  state, creation, name, language, contacts with the instant each was verified, age
  answer (never the date), and the notice and terms versions its collections recorded;
  and its export, `GET .../export?format=`, PRIV-RIGHT-003's routine for the record (its
  sections `record` · `profile` · `identifiers` · `preferences` · `consents` ·
  `objections`, `10` section 5.46, then each export contributor's sections for the
  record's subject), counted against `privacy.export.ratelimit` for the record, with no
  step-up, since the code it was reached by is fresh.
- **Consent and objection.** As `GET /privacy/consents`, the grant and the withdrawal,
  and the objections (PRIV-CONS-008, PRIV-RIGHT-001a), for the record.
- **Rectification.** `PUT /records/access/{subject}` changes the record's name and
  language; a `restricted` record's change is refused with `authz.restricted`. Anything
  else is a request.
- **Requests.** `POST .../requests`, a restriction or a rectification, as
  `POST /privacy/requests` for the record (PRIV-RIGHT-002).
- **Erasure.** `POST .../delete` begins the record's deletion (`deletingBy` `self`,
  `record.deletion.grace`), raises `RecordDeletionRequested` and sends
  `record-deletion-notice` to its notice contacts; `POST .../delete/cancel` inside the
  window returns it as REC-REC-002 states and raises `RecordDeletionCancelled`. While
  its own deletion runs, the record is reached for its read and its cancellation alone.

A record's **notice contacts** are its contacts verified at a collection, or, where none
was, every contact it holds; every message PRIV-RIGHT-002 and this chapter send a record
(a receipt, a lapse, a deletion notice, `oob-deletion-notice`) goes to them, in the
record's language. They are the record's existing holder: `record-deletion-notice` and
`oob-deletion-notice` are security notices, governed only by restrictions of the
`notification` purpose (AUTH-ABUSE-004, D-222).

**Where a record's restriction is enforced (D-222).** The gate judges an account's
restriction, and a record access session acts for no account (AUTHZ-GATE-006). A person
record's restriction is enforced in one place of its own, the record's rights, which
every change a person asks of their record passes, by a code or through the account it
is attached to, and which refuses the change of a `restricted` record with
`authz.restricted`; no other place refuses it. Staff's corrections (REC-ADM-002) are not
the person's change and are admitted, being how a rectification request is fulfilled.
Every action is recorded as the account's same action is, the record acting and its
`subject`.

*Source: D-221*

**Acceptance criteria**
1. A person reaching a record by a code reads it, exports it in both formats with each
   contributor's sections, withdraws a consent and objects to a purpose, each without
   contacting anyone.
2. A record's deletion asked by a code begins its window and sends
   `record-deletion-notice`; a new code reaches it to cancel inside the window, which
   returns it as it was; once the window closes it is erased (REC-REC-003).
3. A `restricted` record's name change is refused with `authz.restricted`, and its
   export is served.
4. A record's deletion asked within a minute of its access code still sends
   `record-deletion-notice`, which answers only to restrictions of the `notification`
   purpose (D-222).

---

## 5. Attaching to an account

**REC-ATT-001** — When an account proves, at that moment, that it holds an email or
phone a person record holds as a contact verified at a collection, the library SHALL
offer to attach those records, and SHALL attach them only when the person confirms.

**Values (D-221).**
- **The proof** is a verification that completes now: the identifiers a registration
  verified, at the terms step that creates the account (REG-SESS-007); an added
  identifier's verification (REG-IDENT-004); a replace's (REG-IDENT-007); or a code the
  account asks for to find its records, `POST /account/records/find` naming one of its
  verified emails or phones, which sends `verification-code` to it under the
  `verification` purpose (a phone only where the phone signal does not answer `risk`),
  and `POST /account/records/find/verify`. An identifier the account verified before
  proves nothing now: a number given up since and issued to another would bring that
  person's records.
- **The offer** is the records holding a value so proven as a contact last verified at a
  collection within `records.proof.age`, `active` and attached to no account, in any
  organization. It is held on the account's session for `records.access.lifetime` from
  the proof, and read at `GET /account/records/offer` as a count and its expiry, nothing
  of the records, before the person confirms. `POST /account/records/attach` attaches
  each record counted, judged again under its lock, and ends the offer. A record older
  than `records.proof.age` is attached only by its reference:
  `POST /account/records/attach/reference`, inside the offer's life, attaches each
  `active` record attached to no account that holds a value the offer proved and that
  reference; one that matches none is counted against the source and the account as a
  wrong code is.
- **Attaching** sets the record's account and instant and nothing else: the record keeps
  its subject, its key, its consents and its organization; nothing is moved,
  re-encrypted or merged (IDN-ACCT-003). `RecordAttached` is raised for each record,
  naming the record as `subject` and the account; `identity.record.attached` records it,
  the account acting, the record its `subject`, filed under the record's organization.
  An attachment is never undone.
- An attachment is admitted only while the account is `active`, read under its lock in
  the attachment's transaction: a `restricted` account's is refused with
  `authz.restricted` (AUTHZ-GATE-006), and an account in any other state with
  `identity.account.stateconflict`, so an attached record never enters a state its
  account has already left (D-222).

*Source: D-221*

**Acceptance criteria**
1. An account that verifies an added phone a record holds as verified at a collection
   inside `records.proof.age` is offered a count of 1, nothing else, and on confirmation
   the record is attached and listed at `GET /account/records`.
2. An identifier verified before a record's collection brings no offer until a code
   proves it again.
3. A record whose collection is older than `records.proof.age` is attached by its
   reference alone, and a wrong reference attaches nothing and is counted.
4. An attached record keeps its subject, and a host field encrypted under it before the
   attachment still decrypts after it.
5. An attachment asked of an account that is no longer `active` when it is read under
   its lock is refused, and attaches nothing (D-222).

---

**REC-ATT-002** — An attached record SHALL follow its account, and its rights SHALL be
exercised through the account.

**Values (D-221).**
- **Its rights.** The code no longer reaches it (REC-ACC-001). The account lists its
  attached records at `GET /account/records` and, under `/account/records/{subject}`,
  reads, rectifies and exports each and manages its consents and objections as
  REC-ACC-002 states, the export a step-up action as the account's own is
  (`privacy:export`, AUTH-STEP-002a); a record not attached to the account is answered
  `identity.record.notfound`. The account's own export carries a section `records`,
  naming each attached record (`10` section 5.46).
- **Its lifecycle.** Every transition of the account into or out of `restricted`,
  `deleting` and `deleted`, a takedown's trigger and its reversal included, applies in
  the same transaction to each record attached to it, with the account's origin and
  window, each record holding what it held and with its own events, outbox records and
  erasures row, as for the record alone: restricting the account restricts each, and
  `RestrictionChanged` names each; the account's erasure erases each (REC-REC-003). A
  restriction follows its **decision**, not the account's state: a restriction decided
  while the account is `suspended` or `deleting`, which the account holds
  (PRIV-RIGHT-004), restricts each attached record at once (holding it where the record
  is `deleting`), with `RestrictionChanged` for each; the account's later return to
  `restricted` changes no record; the restriction's lift lifts each (D-222). An
  account's suspension changes no attached record. An operation that acts on an attached
  record's lifecycle by its own path (a lift, a cancellation, a takedown, an erasure) is
  refused with `identity.record.followsaccount` (409), `details.account` naming the
  account to act on.
- **What a host reads.** `IPersonRecords.AttachedAsync`
  (`GET /admin/accounts/{subject}/records`) answers the subjects of the records attached
  to an account, so a host reads "this person's records" as the account's subject and
  these: a person's own context reads its own; another's is read under `record:read`,
  asked in the organization named (the administrative organization where none is) with
  IDN-READ-004's reach of the account, and answers the attached records that
  organization holds, every one in the administrative organization (D-222).

*Source: D-221*

**Acceptance criteria**
1. Restricting an account restricts each record attached to it in the same transaction,
   and `RestrictionChanged` names each.
2. An account's erasure erases each attached record in the same transaction, each with
   its erasures row and its `ErasureRequested`.
3. A code for an attached record's contact opens a session that does not reach it.
4. A takedown of an account takes each attached record into `deleting` by `takedown`,
   and its reversal returns each to what it held.
5. A restriction decided while the account is `suspended` restricts each attached record
   at once, with `RestrictionChanged` for each; the account's reactivation into
   `restricted` changes no record (D-222).

---

## 6. What staff do

**REC-ADM-001** — Staff SHALL read and find person records under `record:read`, each
read reaching only the records its organization holds or is tied to.

**Values (D-221).** `IPersonRecords.ReadAsync` (`GET /admin/records/{subject}`) answers
the record as REC-ACC-002's read does, with its origin and the account it is attached
to; `IPersonRecords.FindAsync` (`POST /admin/records/find`) answers the subjects of the
records of an organization holding an email, a phone (read in its canonical form,
IDN-ACCT-004) or a reference. A read or an action whose path names a record is asked in
the organization that holds the record, and admitted where its permission is held there
or in the administrative organization, judged as one admission, as D-219 admits an
organization's reads; a caller admitted in neither, and a subject no record bears, are
answered `identity.record.notfound` alike, the first refusal recorded against the
record's organization as IDN-READ-004 records an account read's (D-222). The find is
asked in the organization its body names and answers the records that organization
holds, and, asked in the administrative organization, those every organization holds; an
organization the deployment does not hold answers `identity.organization.notfound`
(D-222). `IAccounts.ContactAsync` and `IConsents.StandingAsync` (IDN-READ-003) answer a
person record's subject as they answer an account's, each answer naming its `kind`
(`account` · `record`), so a host holding a subject reads either in one call: a record's
contact read carries its name, its contacts (its email and its phone as held, verified
or not), its language, no time zone, and its state. A host's declared job may declare
`record:read` (IDN-READ-004). A person's read of a record counts toward their read
volume (OPS-ALERT-005).

*Source: D-221*

**Acceptance criteria**
1. A holder of `record:read` in an organization reads a record it holds, and is answered
   `identity.record.notfound` for one another organization holds, in bytes identical to
   a subject no record bears; a holder in the administrative organization reads both.
2. Finding by a phone written with Arabic-Indic digits answers the record holding its
   E.164 form; finding by a reference answers the records holding it.
3. The contact read of a record's subject answers its name, contacts, language and state
   with `kind` `record`.

---

**REC-ADM-002** — A person record's rights SHALL be exercisable through staff, for the
person who reaches it by no code, under `record:manage`.

**Values (D-221).** For a record attached to no account (an attached one answers
`identity.record.followsaccount`, REC-ATT-002):
- **Requests.** `POST /admin/privacy/requests` names a record's subject as it names an
  account's (PRIV-RIGHT-001, PRIV-RIGHT-002). Fulfilling an erasure takes an `active` or
  `restricted` record into `deleting` by `oob-request` (holding the restriction), sends
  `oob-deletion-notice` to its notice contacts, and raises `RecordDeletionRequested`;
  one already `deleting` or `deleted` is recorded fulfilled as for an account
  (IDN-LIFE-003). A restriction granted, or by lapse, makes the record `restricted`, or
  holds the restriction where it is `deleting`.
- `POST /admin/records/{subject}/restriction/lift` lifts a restriction (step-up
  `record:restrictionlift`); `POST /admin/records/{subject}/delete/cancel` cancels a
  deletion begun by `self` or `oob-request` (step-up `record:deletioncancel`); a
  takedown's answers `identity.takedown.active`.
- **Access.** `GET /admin/records/{subject}/export?format=` assembles the record's
  export (REC-ACC-002) for staff to hand to the person (step-up `record:export`),
  recorded `privacy.export.assembled` with the staff member acting.
- **Rectification and consent.** `PUT /admin/records/{subject}` corrects the name,
  language, email and phone, with a `reason`; a contact changed is unverified from then
  on, so a code never reaches a contact staff wrote.
  `POST /admin/records/{subject}/consents/{purpose}/grant` · `/withdraw` and `POST` ·
  `DELETE /admin/records/{subject}/objections/{purpose}` record what the person told
  staff, with the mechanism `administrator` and a `reason` (D-223). A grant or an
  objection staff enter is a record of its own whose mechanism is `administrator`
  (PRIV-CONS-001); a withdrawal stamps its instant on the record it ends, which keeps
  the mechanism of its making. Staff's part is the audit entry's: the staff member
  acting, the record the `subject`, `details.mechanism` `administrator` and
  `details.reason` the reason given (`10` section 5.24), on every one of the four.

*Source: D-221, D-223*

**Acceptance criteria**
1. An out-of-band erasure request fulfilled for a record begins its window and sends
   `oob-deletion-notice` to its notice contacts; cancelling it returns the record as it
   was.
2. A contact staff correct is unverified afterwards, and a code to it reaches nothing.
3. A consent withdrawn by staff stamps its record withdrawn, is audited
   `privacy.consent.withdrawn` with the staff member acting, `details.mechanism`
   `administrator` and `details.reason`, and raises `ConsentChanged` `withdrawn`; a
   consent staff grant is a record whose mechanism is `administrator`.

---

**REC-ADM-003** — A host SHALL be able to erase a person record it no longer keeps
anything about, under `record:erase`.

**Values (D-221, D-223).** `IPersonRecords.EraseAsync`
(`POST /admin/records/{subject}/erase`), under `record:erase` in the record's
organization, with a required `reason`, erases an `active` or `restricted` record at
once (REC-REC-003, reason `retention`); one `deleting` or `deleted` is refused with
`identity.record.stateconflict`, one attached with `identity.record.followsaccount`. The
erasure's audit entry, `privacy.erasure.executed`, carries `details.reason` `retention`
in its section 5.12a spelling, as every erasure's does, and the reason given in
`details.statedReason`, whether a person or a job gave it. A host's declared job may
declare `record:erase`, so the host's retention job erases a record when the last thing
it kept about the person has gone (PRIV-RET-001). Nothing erases a record on its own:
the library holds no host table and cannot know whether the host still keeps data under
the record's key (LIB-HOST-002).

*Source: D-221, D-223*

**Acceptance criteria**
1. A job declaring `record:erase` erases an `active` record outside a request; the
   erasure is audited with its principal, `details.reason` `retention` and the reason
   given in `details.statedReason`, and its erasures row carries `retention`; a staff
   member's erasure is audited the same way, the staff member its principal.
2. An erasure of a record that is `deleting` is refused with
   `identity.record.stateconflict` and changes nothing.

---

## 7. The takedown

**REC-TKD-001** — The takedown of IDN-LIFE-003 SHALL apply to a person record, under
`takedown:execute`.

**Values (D-221).** `POST /admin/records/{subject}/takedown`, with `trigger` and
`reason` as for an account (`10` section 5.12d), takes a record that is `active`,
`restricted`, or `deleting` by `self` or `oob-request` into `deleting` by `takedown`,
holding what it held, ends every record access session reaching it, and writes the
`TakedownExecuted` outbox record naming its subject, in one transaction (step-up
`record:takedown`); no `AccountSuspended` is written, a record having no access to
suspend. The window, the earlier end for a record already deleting, the erasure when the
window closes, the reversal at `POST /admin/records/{subject}/takedown/reverse` (step-up
`record:takedownreverse`) and the read of its progress at
`GET /admin/records/{subject}/takedown` are an account's (IDN-LIFE-003), with an
account's codes (`identity.takedown.active`, `identity.takedown.windowelapsed`,
`identity.takedown.notfound`) and `identity.record.stateconflict` for a `deleted`
record. A record attached to an account is taken down with it, and a takedown naming it
is refused with `identity.record.followsaccount`.

*Source: D-221*

**Acceptance criteria**
1. A takedown of an `active` record takes it into `deleting` by `takedown` in one
   transaction, ends its access sessions, writes `TakedownExecuted`, and no code reaches
   it afterwards.
2. Reversed inside its window, the record returns to what it held; not reversed, it is
   erased when the window closes.
3. A takedown naming an attached record is refused with `identity.record.followsaccount`
   naming the account.

---

## 8. Open items

None. Cross-references: the account and its takedown in `01-identity`; the sensitive
registration rule in `03-authorization` AUTHZ-INHERIT-002; consent, rights and keys in
`04-privacy`; the endpoints in `09-api-contract` sections 7a and 8a; names, codes, keys
and events in `10-reference`; the screens in `18-frontend-integration` FE-REC-001 to
FE-REC-003; the attachment's offer at registration and at an identifier's verification
in `20-registration-and-account`.
