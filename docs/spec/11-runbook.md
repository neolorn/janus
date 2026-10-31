# 11 — Runbook

The library's operational procedures: the break-glass credential, admin-assisted
recovery, the library's outside dependencies, security incidents and configuration
changes. A deployment's own procedures (deploying, backups, a company's calendar and
staffing) are the host's (D-225).

---

## 1. Standing facts

*Retired by D-225. Who holds which role, and how a deployment is run, are the host's.*

---

## 2. Deployment

*Retired by D-225. Deploying is the host's.*

---

## 3. Break-glass

### 3.1 When to use it

When **no system administrator can reach the system** — the operator is unreachable,
or the operator has lost their own credentials. For a lone administrator the second
case is not "administrative recovery" (section 4): recovery needs an approver who is
not the person being recovered, and there is only one administrator. **Break-glass is
the recovery path for the sole administrator.** A staff member who loses a passkey
while the administrator is reachable is section 4.

### 3.2 Procedure — written for the owner

You do not need technical knowledge for this. Read it through once before starting.

1. **Get the emergency code** from where your deployment keeps it.
2. **Open the emergency address** your deployment gave you (its `/break-glass` page,
   FE-BG-001) in any web browser. You will see two boxes and one button.
3. **Type or paste the emergency code into the first box, write in the second box why
   you are using it, and press the button.** The reason is required; the system keeps it
   with every action you take in the session. The code works once; after this it is
   spent. If the page says the code is invalid or already used, stop and follow your
   deployment's own procedure. If it asks you to wait, wait until the time it shows.
4. **You are now signed in with full administrative access for a fixed period**
   (`breakglass.session.lifetime`, 4 hours unless the operator has changed it; the page
   shows when it ends), and the management application opens. At that moment the system
   raises an alert, which your deployment delivers to the operator and to you; that is
   expected. The session also ends if you leave it unused for an hour
   (`session.aal2.inactivity`, unless the operator has changed it), and an ended session
   cannot be resumed: do step 6 before you step away.
5. **Do what the emergency needs.** Usually one of: approve the pending recovery so the
   operator (or a staff member) can get back in; or give administrative access to
   someone you trust who can reach the system.
6. **Before you finish, make a new emergency code.** In the management application, open
   the emergency-access page and use the control that generates a replacement credential
   (OPS-BOOT-004; the labels shown there are illustrative). Keep it as your deployment
   keeps the old one. Without this step there is no emergency code until the operator
   returns. Making the new code raises an alert as well; that is expected.
7. **Write down what you did and when.** The system records every action taken in the
   session; your note is for the review afterwards.

The session ends on its own when that period is over (`breakglass.session.lifetime`,
default 4 hours), or after an hour unused. Nothing you did is undone by that.

*Source: OPS-BOOT-002, OPS-BOOT-004, D-010, D-103, D-129, D-147, D-166, D-225*

### 3.3 Annual reseal

*Retired by D-225. The custody of the sealed envelope is the host's; the commands it
uses stay (`06` OPS-SEC-003, OPS-BOOT-004).*

---

## 4. Admin-assisted account recovery

For anyone who has lost their credentials and cannot use self-service recovery: a
staff member (who never can), a customer whose mailbox is gone (D-111), or a customer
whose phone is gone and who cannot use self-service (AUTH-RECOV-002, D-147, D-148).
Every human account holds a verified email (REG-IDENT-001), so a phone is never the
only channel; within the re-enrolment session the approver's confirmation stands in
for the old address (REG-IDENT-007).

1. Confirm identity **out of band, on a channel already recorded on the account** —
   never one supplied in the request (AUTH-RECOV-003).
2. Record which channel was used.
3. Record a written reason. Mandatory.
4. Issue the time-boxed enrolment link **to a recorded channel**: for a customer
   whose mailbox is gone, their recorded phone; for a customer whose phone is gone,
   their recorded email. If the approval is answered with a wait (a sending
   restriction on that channel, or the approval limits of AUTH-RECOV-002), nothing was
   approved and no link went: approve again once the time shown has passed.
5. The account owner is notified on a separate channel automatically.
6. If the person's old mailbox is unreachable, the re-enrolment session lets them set
   a new email address; the old address is notified without a link and the remaining
   channels hold the undo for `identifier.change.coolingoff` (REG-IDENT-006, REG-IDENT-007).
   If the lost channel is the phone, the same session lets them replace the phone
   number, confirmed by the new number alone, with the notice and the undo going to
   the recorded email (REG-IDENT-007, AUTH-RECOV-002).

**Do not** accept a phone number or address provided during the request. The
pre-existing channel is the entire control.

Approver anomaly detection runs per approver (AUTH-RECOV-002). With one approver,
the operator reviews the approver report weekly, rather than relying on it to alert
(D-147).

**An organization no one can manage.** An organization `POST /admin/organizations`
creates holds no grant, and one whose last holder of `grant:manage` has left has no one
to grant. A system administrator writes its grants from the management application
(`POST /admin/grants`, under `system:administer`, with a reason): `grant:manage` and
`membership:manage` to a staff member who answers for it (AUTHZ-SCOPE-001, D-207).

---

## 5. Minor discovered — takedown

*Retired by D-225. The procedure is the host's (`14`); the takedown itself is `01`
IDN-LIFE-003.*

---

## 6. Data loss and corruption

*Retired by D-225. Backups and restores are the deployment's; after a restore the
erasure ledger is replayed (`12` DR-016).*

---

## 7. Provider failures

### 7.1 Mail

*Retired by D-225. Message delivery is the host's; a mailbox push that fails is
OPS-OBS-002's.*

### 7.2 SMS gateway

*Retired by D-225. The library talks to no gateway.*

### 7.3 Password screening

Unreachable → the offline fallback runs and the degradation is logged. Both
unavailable → password set and change **fail rather than accept unscreened**
(AUTH-PASS-004).

Do not disable screening to restore service. Blocklist screening is the load-bearing
control now that composition rules are gone.

### 7.4 Social sign-in providers

Unreachable or unreadable (`degradation` under `provider.unavailable:<provider>`,
`details.part` `discovery`, `keys` or `token`; a token endpoint's 5xx or 429, its
refusal of the deployment's own client, and a discovery document naming no HTTPS
authorization or token endpoint included): the person is returned with
`auth.provider.unavailable`; nothing is counted or changed. Where the token endpoint
refuses the deployment's client (`invalid_client`), the fault is the deployment's: check
the credential the secret source gives for the provider, a revoked key or a rotated
secret; correct it there and restart the application, which reads it at start
(IDN-LIFE-012, D-193). Those whose only way in is that provider must wait; everyone else
can use another factor. Do not remove the provider's declaration to quiet the alert: a
provider the deployment does not declare answers `auth.factor.notpermitted` at the round
trip, and its security events are refused, Google's with `invalid_issuer` and Apple's
with `integration.callback.rejected` (`09` section 10, IDN-LIFE-012a, D-192).

### 7.5 Sign-on

An application answering 500 `system.fault` at `GET /auth/signon` or its return is a
fault: the authentication application unreachable or failing (a 5xx, a push or exchange
answer not in its protocol's shape, `server_error` or `temporarily_unavailable` on the
authorization response), the push or the exchange refused for the deployment's own
client or request, the client in no registry, its secret or the application's published
keys that cannot be read, an identity token that does not hold up or carries no session
identifier in `sid` (D-196), a derivation code that is a fault's, or no
pre-authentication session issued. Read the fault's logged detail by its correlation
identifier: its type and frames; the code and details it carries where it carries one
(BFF-ERR-002), which for a secret or keys that cannot be read are those their read
answered; for an identity token that does not hold up, the type of its inner fault,
which names the check that failed; and, where the push, the exchange or the
authorization response answered, the status and `error` read (D-195, D-196). A client in
no registry and a token carrying no session identifier in `sid` are each named by an
entry at Error beside the fault (D-196). Check that the client the host declares
(`SignOnClient`) is registered with its exact `redirect_uri` (LIB-HOST-001). An
authorization response's `error` comes through the browser, so a `server_error` or
`temporarily_unavailable` while the authentication application is otherwise healthy may
be a person's own edit of their return, which faults that person's sign-on alone
(D-196). A sign-on that keeps returning `auth.session.expired` logs, at Information, the
code that refused it inside, an inside `api.request.malformed` being a return carrying
neither `code` nor `error`, or a value that does not read, an authorization response's
`error` outside its protocol's characters or over 64 characters among them; read that
the same way (BFF-SESS-006, BFF-LOG-001, D-194, D-195, D-196).



## 8. Security incidents

### 8.1 Suspected session compromise

Use the explicit revocation operation (AUTH-SESS-009). This is distinct from the
automatic downgrade that follows a policy change — revocation is immediate and
total. Ending another person's sessions
(`POST /admin/accounts/{subject}/sessions/revoke`) asks for step-up
(`account:sessionsrevoke`), and so does ending every session of the deployment
(`POST /admin/sessions/revoke-all`, `session:revokeall`).

### 8.2 Suspected credential compromise

Revoke sessions, then require re-enrolment. Password rotation is required **only on
evidence of compromise** (AUTH-PASS-003) — do not impose blanket rotation, which
produces predictable derived passwords.

### 8.3 Personal data breach

**Clock starts at detection**: 72 hours to the regulator, three days to affected
subjects (PRIV-BREACH-001). **Not deferrable pending investigation** — if you are
uncertain whether an incident is notifiable, the clock is already running while you
decide. The three-day subject clock is this system's conservative choice; the law
counts it from the notification to the Centre (D-125) — if the 72 hours are used, the
legal deadline for subjects is later than ours, never earlier.

For a minor takedown, detection is the moment of the credible indication, not the later
conclusion that it was notifiable.

1. Determine scope. Audit records are queryable by data subject for this purpose
   (PRIV-BREACH-002).
2. Notify the regulator within 72 hours.
3. Notify affected subjects within three days. **Non-suppressible** — no preference
   silences it.
4. Record the timeline.

Notification is not optional and not deferrable pending investigation. Notify with
what is known.

---

## 9. Configuration changes

**Tightening** a security control needs no step-up, but like every change it carries a
written reason and produces an audit entry (OPS-CFG-002, OPS-CFG-008).

**Loosening** requires step-up, `system:administer`, a written reason, and produces an
audit entry (OPS-CFG-002).

**Protected settings** (`10-reference`, section 4.8) cannot be changed through the
application at all: they require access the application does not have. They are
changed from the server with the `configure` command of `Janus.Cli`, which takes a
reason and raises a High alert for each key (OPS-CFG-004). A change of the mail server
adapter's endpoint (`integration.mailserver.endpoint`) made with `configure` takes effect
when the application next starts, since the mail server in use is chosen at the start
(CONV-DESIGN-007). Audit logging, token signature verification and step-up enforcement
have no switch at all. If an incident appears to require disabling rate limiting or
export auditing, that is the moment those protections exist for. They are on the list
precisely because turning them off would blind the system to whoever turned them off.

*Source: OPS-CFG-002, OPS-CFG-004, OPS-CFG-008, D-166, D-176*

---

## 10. Compliance calendar

*Retired by D-225. A company's calendar is its own.*

---

## 11. What changes when someone joins

*Retired by D-225. A company's staffing is its own.*

---

## 12. Open items

*Retired by D-225. The one open item, the gateway's plain-HTTP report, lapsed with the
report route.*
