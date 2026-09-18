# Janus, explained

A plain-language description of the whole system as it will behave once the decisions taken up to 10 September 2026 are written into the specifications. The specifications remain the source of truth; this document never states a rule they do not. It is kept in step with them: when a decision changes behaviour, this document changes with it.

Throughout, the person is **Hossam**. He appears as a customer of an online store, as a staff member invited to join the company that runs it, and once as the owner who keeps the emergency envelope. The store is an example host; Janus itself knows nothing about stores.

**How to read it.** Sections 1 to 3 are the account and how one is created. Sections 4 to 6 are daily use: signing in, staying safe, managing your own account. Section 7 is the organisation side (staff, roles, permissions). Section 8 is what the business can configure. Section 9 is a short look behind the curtain. Section 10 is what happens when things go wrong. Every unavoidable technical term is explained in the sentence where it first appears; the appendix collects them.

---

## 1. What Janus is

Janus is a software library. A company's own application (the "host") imports it and gets three things without building them: **identity** (who the accounts are), **authentication** (proving it is really you) and **authorisation** (what you are allowed to do). It is not a separate service to run; it lives inside the host application, with its own database tables that the host never touches directly.

It is written to be **generic**. It has no idea what a store, an order or a medical product is. The host tells it, at start-up, what kinds of things exist ("orders", "branches"), what the processing purposes are ("fulfilment", "marketing"), and which policies apply. Two entirely different businesses run the same Janus without a line changed.

What it deliberately does not do: merge two accounts into one, let one person act as another, host third-party apps, or store payment card details. Each of those is either out of scope or handled by someone else (payments by the payment provider, shipping by the courier, mail by the mail server).

Three principles shape everything below:

| Principle | What it means for the business |
|---|---|
| Secure by default | A deployment that forgets to configure something ends up stricter, never looser. Tightening a setting is free; loosening one needs a strong re-authentication, a written reason and an audit entry. A few settings cannot be changed from the application at all. |
| Zero maintenance | Nothing needs a recurring calendar entry, with two named exceptions: resealing the emergency envelope once a year, and renewing the regulatory licence and permits. Everything else is automated or alerts when it needs a human. |
| Never punish the well-behaved person | Every rule is written so the honest person gets through with the least friction the rule allows. Where a rule is strict, it is strict on the attacker's side of the door. |

Language: English first, Arabic native and first-class. Every legal document (terms, privacy notice, consent texts) exists in one **governing language** the host declares (Arabic for this deployment), which prevails if versions differ; translations are optional and can be read from within any document without switching the app's language.

---

## 2. Hossam's account: what Janus holds

An account is a person, once. Google or Apple sign-in attaches to an account; it never creates a second one. Accounts are never merged.

| Held | Required | Who can change it | Gone when the account is erased |
|---|---|---|---|
| Account identifier | Always | Nobody | Survives as an anonymous row, so audit history still makes sense |
| Email addresses (one or several) | At least one, always | Hossam, with re-confirmation | Yes |
| Mobile numbers (one or several) | One, by default (the business can make it optional) | Hossam, with re-confirmation | Yes |
| Adult confirmation (18+) | Where the business serves adults only | Nobody (derived from the date of birth entered at sign-up) | Not a personal detail, so it is not what erasure removes |
| Date of birth | Only if the business decides to keep it (off by default) | Support only | Yes |
| Display name | Optional | Hossam | Yes |
| Legal name | Only if the business needs it (off by default) | Hossam | Yes |
| Username | Only if the business turns usernames on (off by default) | Hossam, with a 30-day wait between changes | Open point (section 11) |
| Photo | Only where policy allows (staff by default) | Hossam | Yes |
| Language, time zone, appearance and other preferences | Optional, sensible defaults | Hossam | Yes |
| Sign-in methods (passkeys, password, security keys, authenticator app, Google/Apple links) | At least one way in | Hossam | Deleted |
| Consent and objection records | None required | Hossam, from the privacy page | Kept as evidence, no longer tied to a readable person |

Things Janus never holds: delivery addresses (the host keeps those, per order), card details, government ID numbers, precise location. Everything personal is encrypted under a key that belongs to Hossam alone; erasing him means destroying that key, which makes every personal field unreadable in one stroke, including fields in the host's own tables.

**Several emails and phones.** Hossam can keep more than one of each, all confirmed. One of each kind is **primary**: that is where ordinary mail and order updates go. Security notices (someone changed your password, someone signed in from a new device) go to a set Hossam chooses: primary only, primary plus one backup, or every confirmed address. The same set is what can recover the account if he is locked out. Any confirmed address can be used to sign in.

---

## 3. Getting an account

### 3.1 Hossam registers at the store

He clicks "Create account" at checkout. Nothing is saved as an account until the very end; until then it is a registration in progress, tied to his browser, which expires if abandoned. There is no Back button; anything he needs to correct has its own Change control where it appears.

| Step | What Hossam sees | What happens underneath |
|---|---|---|
| 1. Get started | His date of birth. That is all. | If the business serves adults only and the date makes him under 18, the flow ends here, before any email or phone is asked for, and the fields lock so a different year cannot simply be typed. If the business serves minors, the same screen just records his age group. The date itself is kept only if the business has said so; by default only the answer is kept. |
| 2. Email | His email address, or Continue with Google / Apple. | A typed address will get a code. A Google or Apple address needs no code when Google or Apple runs that mailbox (Gmail, iCloud); if the Google account was created with, say, an Outlook address, one code is sent like any other. |
| 3. Phone | His mobile number, with a country picker. | Required by default; the business can make it optional. He is warned to check it, because texts to a number are limited. One number belongs to one account. |
| 4. Confirm | Every address and number listed, each with six boxes for its code. | Each gets a code and a link. Typing the code confirms it, and so does opening the link in this browser and pressing the button on it (merely opening does nothing, because mail systems open links automatically to scan them). Opened on another device, the link shows the code to type back here, with a "This wasn't me" button that cancels the whole registration. Here he can Change an address or number (a fresh code goes to the new one), add another email or phone, or remove an extra. |
| 5. Security | How he will sign in. | A passkey (fingerprint, face or device PIN; the strongest and simplest option), or a password, or the Google/Apple account he already used. A password must be 15 characters, or 10 with a second step (authenticator app or security key) set up right there. Nothing else is imposed on a password; see 5.3. If he sets up a second step, ten recovery codes are shown once with Copy, Download and Print, and he confirms he has saved them before continuing. |
| 6. Terms | Agree to the terms; the privacy notice to read; two optional switches (offers by email, personalised recommendations), off. | Clicking Create account creates the account, all at once. Consent for recommendations is recorded separately as written consent, because purchase history can reveal health information. The notice is recorded as shown, not agreed. |
| 7. About you | Display name, time zone (picked up from his device), photo where allowed, legal name or username if the business asks. | All optional, all editable later. |
| 8. Preferences | Language, appearance, text size. | Follow him to every app of the business. |
| 9. Membership | Only for invited staff (see 3.2). | Skipped for customers. |
| 10. Done | A summary of everything he set up, and a button back to the store, signed in, basket intact. | The store is told where to land him by its own registered address, never by a link in the request. |

**What can go wrong, and what he sees**

| Situation | What Hossam sees | What actually happens |
|---|---|---|
| He types an address that already belongs to someone | Exactly the ordinary "code sent" screen | No code is sent. The owner of that address gets a message: "someone tried to register with your address; if it was you, sign in instead". Nobody can use registration to find out whether an address is on file. |
| He mistypes his number | Change it; a fresh code goes to the new number at once | A corrected number is a different number, so the limit on the old one does not apply. |
| The text does not arrive | "Sent to +20 ...; texts usually arrive within a minute", and when the limit for that number lifts | Each number gets a small number of texts (3, regaining one per day, by default); a bounced text does not count. If someone else burned his number's allowance, support can grant one more send. See 8.2. |
| He types the wrong code | "That code isn't right, 4 tries left" | Five wrong tries invalidate the code; a new one must be requested. |
| He picks a weak password | The meter says why it is weak | It is refused only if it is too short or on the list of leaked passwords. Otherwise it is his choice; see 5.3. |
| His Google account already has an account here | "You already have an account; sign in" | That is a sign-in, not a registration. |

### 3.2 Hossam is invited to join the company

Mona, in Operations, invites Hossam to join the company as order-handling staff. The invitation email goes to his personal address. The company has decided what the invitation carries:

| The invitation can... | Effect on Hossam |
|---|---|
| Name his work email | The address is shown and locked. Where the company's own mail server creates the mailbox (the case for the company that runs Janus), it needs no confirmation: the company gave it to him, and his personal address is what proves it is him. An organisation whose mail lives elsewhere has him confirm it by code like any address. |
| Leave the work email open | He types one himself. If the company has a **domain lock** (a list of its own verified domains), it must end in one of them. It is confirmed by code like any address. |
| Name his mobile number | Shown, locked, and confirmed by a text, so the invitation link alone is not enough to accept. |
| Leave the number open | He types one. |

The flow is the same ten steps with three differences. His personal address, the one the invitation was sent to, is kept on the account as a verified second email: he cannot sign in with it while he is staff (the domain lock allows only company addresses), but it receives security notices, and it is what keeps his account alive if he ever leaves. At the security step the company's policy applies: passkeys only. If his passkey is synced (kept in iCloud Keychain, Google Password Manager or a password manager, so it survives a lost device) one is enough; if it lives only on one device, he must add a second credential (another device or a hardware key) so a lost phone never locks him out. The membership step appears: his roles, the documents he must read (recorded with their versions), and, once he confirms, his work mailbox is switched on with a button to open webmail and, if he wants mail on his phone or in Outlook, an **app password**: a separate password for mail programs that cannot use passkeys, shown once, revocable any time from his account, and the weakest credential in the system for exactly that reason.

If the invitation's bound address or number belongs to someone else's account, he sees the ordinary screen and that person is told. What happens when Hossam already has an account (as a customer, say) and accepts an invitation is an open point; see section 11.

### 3.3 Signing up with Google or Apple

"Continue with Google" replaces the email step: Google tells Janus the address, and if Google runs that mailbox the address counts as confirmed. Everything else is unchanged: phone, security (he can add a passkey or password, or rely on Google alone), terms. An account that has only Google is signed in on Google's word; the first time Hossam does anything sensitive he is asked to add a passkey or password of his own.

---

## 4. Signing in

### 4.1 The ways in

| Method | What it is in plain words | How strong | Notes |
|---|---|---|---|
| Passkey | A credential stored on his phone, laptop or password manager, unlocked with fingerprint, face or PIN | Strong (two factors in one) and phishing-proof | The recommended way. Can also live on a hardware key. Synced passkeys (iCloud, Google, 1Password) survive a lost device; device-bound ones do not, and the account says which is which. |
| Password | The familiar secret | Basic alone; strong with a second step | 15 characters alone, 10 with a second step |
| Google or Apple | Google or Apple vouches for him | "Delegated": signed in, but Janus makes no claim about strength | Never asks for a second step; Google's own security is trusted for that |
| Email link or code | A link or code sent to a confirmed email | Basic | Off by default; a business must turn it on |
| SMS link | A link or code texted to a confirmed number | Basic, and flagged "less secure" | Off by default. Texts are limited (see 8.2) |

And the second steps, used beside a password:

| Second step | What it is | Strength of password + this |
|---|---|---|
| Authenticator app | A six-digit code from an app such as Google Authenticator or 1Password, changing every 30 seconds | Strong |
| Security key | A hardware key (YubiKey and similar) touched after the password | Strong and phishing-proof |
| SMS code | A texted code | Strong by the standard's accounting, but flagged "less secure" because phone numbers can be hijacked; off by default |
| Recovery code | One of the ten saved codes, each usable once | Stands in for a lost second step |

Every credential has a name Hossam chooses ("Chrome on Windows", "YubiKey 5C"), shows when it was added and last used, and can be removed. A security key registered as a second step can be upgraded into a passkey. If he has more than one second step, he picks which is offered first.

### 4.2 What happens after the password

Signing in with a password is followed by the second step if he has one. Then two protections apply that he mostly never notices:

**New device check.** If Hossam has no second step and no passkey (just a password, or just Google) and signs in from a browser the account has never seen, a code is emailed to his primary address and must be entered before the sign-in finishes. That browser is then remembered (90 days by default). Someone who has stolen his password but not his inbox stops here. Passkey and two-step sign-ins never see this check.

**Trusted device.** After a full two-step sign-in, Hossam can mark the browser trusted, and for 30 days it skips the second step (the password is still always required). Every trusted device is listed in his account and can be removed; all of them are dropped when he changes his password, signs out everywhere, recovers the account, or types the password wrong three times on that device. The offer is not made while his password is shorter than 15 characters, and staff never get it.

### 4.3 How long a session lasts

| Who | Signed in until | Even if active, at most |
|---|---|---|
| Customer | 90 days without using it | 365 days from sign-in |
| Staff | 1 hour without using it | 24 hours from sign-in |

A staff member who goes idle inside the 24 hours just taps a passkey and carries on where they were; unsaved work is not lost. Signing out ends every app of the business at once. Every session is listed in the account with its device, approximate city, when it signed in and when it was last used, and each one can be ended individually; "sign out everywhere" ends all of them.

### 4.4 What an attacker sees

Nothing that distinguishes an existing account from a non-existent one. Wrong passwords meet a growing delay, never a lockout (a lockout would let anyone who knows Hossam's email lock him out). The delay message is identical whether or not the account exists. Asking for a recovery link for an address with no account emails that address to say so. Bot checks appear only on suspicious signals; an ordinary person never sees one.

---

## 5. Staying safe

### 5.1 Sensitive actions

Some actions are sensitive: changing an email address, removing a passkey, exporting data, anything administrative. Each is behind a **gate** that declares what it needs: how strong the sign-in must have been, whether it must have been phishing-proof, and how recently (15 minutes by default). If Hossam's session already meets that, nothing is asked. Otherwise he is asked to confirm with one of the methods he holds, in place, without losing what he was doing.

A gate never ends in a bare "no". If Hossam has never had what the gate needs, he is invited to set it up right there. If he once had it and lost it, he is invited to report the loss (5.2). If a loss report is already running, he is told when it completes. For customers the gate asks only for what their own account can reach: a password-only customer passes every gate with the password; a customer who set up an authenticator app is asked for it. Staff gates always require the strong, phishing-proof level. An SMS code satisfies a gate that asks for the strong level but never one that asks for phishing resistance; a business that wants sensitive actions kept away from SMS marks those gates phishing-resistant.

### 5.2 Losing a device

Every credential is in one of three states: active, suspended, or gone. When Hossam loses his phone with his authenticator app:

1. He signs in with what he still has (his password and a recovery code, say) and reports the app lost. It is suspended at once: it no longer works for sign-in or for any gate.
2. Every address in his security-notice set is told, repeatedly, each message carrying a cancel link. Cancelling from any of them restores the credential, in case it was found or the report was a mistake.
3. After 7 days (by default) with no cancellation, the credential is gone for good. During those 7 days the account is treated as still holding it, so gates that needed it stay closed to everyone, including a thief who has reset the password.

Recovery codes: ten, shown once when a second step is set up, each usable once, replaceable any time with a fresh set (which kills the old one). The account records whether they were viewed and saved and reminds him after a year if they never were.

**Forgot the password.** A customer asks for a reset link; it goes to the security-notice set. The link sets a new password and nothing else: it never removes a second step, so someone who has only broken into the mailbox still cannot get past the authenticator app. A customer whose only way in was a lost passkey uses the same link to set a password, signs in, and then reports the passkey lost as above. Staff have no password to reset.

If none of that helps (no device, no codes, no email), there is admin-assisted recovery: a person with the recovery-approval permission confirms Hossam's identity on a channel already on the account, never one he supplies in the request, writes a reason, and sends him a single-use link to set a new passkey or password. Staff never get self-service recovery; they are required to hold a synced passkey or two credentials from day one, and otherwise use the admin path.

### 5.3 Password rules

Exactly three, and nothing else:

| Rule | Detail |
|---|---|
| Length | At least 15 characters alone, or 10 with a second step. Up to 128. Spaces and any language allowed. |
| Not on the leaked list | Checked against a list of passwords known to have leaked in breaches, every time a password is set or changed. If the online check is unreachable an offline copy is used; if neither works, the change fails rather than skipping the check. |
| That is all | No "one capital, one digit, one symbol". No expiry. No hints or security questions. |

A strength meter shows why a password is weak (repeats, sequences, a common word, his own name or email) but never blocks; that is his choice. A business that wants stricter blocking can switch on extra lists, off by default.

### 5.4 Who else can get in

Nobody, by design. Support staff can grant an extra text to a number, approve a recovery, or reverse a mistaken takedown, each under their own permission, each strongly re-authenticated, each audited with a reason. No staff member can read a password (none is stored, only a one-way hash) or sign in as Hossam.

---

## 6. Hossam's own controls

Everything below is self-service from his account. Sensitive ones ask him to confirm first (5.1).

| Control | What he can do | Protection |
|---|---|---|
| Emails and phones | Add (confirmed by code), remove, choose the primary, choose the security-notice set | Add and remove are sensitive. Removing is immediate; the remaining addresses get an "undo" link for a while, so a removed address, if it was removed because it was compromised, can never undo its own removal. The primary cannot be removed until another is primary. Changing the security-notice set tells every address currently in it. |
| Sign-in methods | Add or remove passkeys, change the password, add or remove second steps, link or unlink Google and Apple, rename anything, upgrade a security key to a passkey, choose the preferred second step | Changing the password ends every other session. Removing the last strong credential goes through the 7-day suspension in 5.2 rather than an instant removal. |
| Sessions and devices | See every session and trusted device, end any one, sign out everywhere | |
| Recovery codes | View status, generate a new set | |
| Profile and preferences | Display name, legal name or username where enabled, photo, time zone, language, appearance | Date of birth, where kept, is corrected through support only. |
| Deactivate | Switch the account off without deleting it; nothing is lost and a link in the notice switches it back on | Reactivation by Hossam only; an account an administrator suspended is restored only by an administrator. |
| Privacy page | See and change every consent and objection; download his data; ask for a correction; ask for processing to be paused; delete the account | See 6.1. |

### 6.1 Privacy, from Hossam's side

The business processes his data for named purposes, each on a legal footing the business declared: fulfilling his orders (contract), keeping tax records (legal obligation), sending offers (consent), personalised recommendations (his explicit written consent, because purchases can reveal health information). Consent is a record, one per purpose, never pre-ticked. Withdrawing takes as many clicks as granting, needs no approval, and anything held only for that purpose is erased; the record that he once consented is kept as evidence. For purposes that do not rest on consent but can be objected to, objecting is a right of its own: no grounds, no approval, always honoured.

| Right | What happens | Time limit |
|---|---|---|
| See or download his data | One export, machine-readable or readable | Immediate |
| Correct something | Editable things he edits; the rest goes to support | Decided within 6 working days |
| Pause processing | His records stay visible but nothing acts on them (no shipping, no refunds, no contact) until resolved | Decided within 6 working days; if nobody decides, the pause is granted automatically |
| Delete the account | A 30-day grace window (by default) in which he can change his mind; then erasure: his key is destroyed and every personal field becomes unreadable, while receipts survive with no buyer | 30 days |
| Be told of a breach | Within 3 days of the business detecting it; cannot be silenced by any preference | 3 days |

Six working days is counted on the business's own calendar (Sunday to Thursday plus a holiday list the business maintains). The system sends a receipt, warns staff two working days before the deadline and again on the day, and records what the law makes of a missed deadline.

There is no cookie banner: the only cookies are the ones the service cannot work without, and the privacy notice lists each of them.

---

## 7. The organisation side

### 7.1 Staff are members, not a type of user

There is one pool of accounts. An **organisation** is a group inside it, and the company that runs the store is organisation number one, the administrative one; its members are what other systems call staff. Hossam-the-customer and Hossam-the-staff-member are the same account; what differs is that a **membership** links him to the organisation, and the organisation's policy then applies to him: passkeys only, a synced passkey or two credentials, sessions of 1 hour idle and 24 hours absolute, every sensitive action requiring the strong phishing-proof level, no Google or Apple, no trusted devices, no self-service recovery. Whatever he set up as a customer that the policy forbids (a password, say) stops working for sign-in but is not deleted.

A second organisation with different rules is a configuration row, not new software. By default an account may belong to one organisation; allowing more is a switch.

### 7.2 Who may do what

Every permission in the system is one sentence: **someone has a role on something.**

- *Someone* is a person or a group. Groups nest: Hossam is in "Cairo support", which is in "Support".
- A *role* is a named bundle of actions ("order handler", "reader"), stored as data, editable at runtime without a release.
- *Something* is one record, a container (a branch, a folder), or the whole organisation. Access to a container flows down into everything it holds.

A **deny** is the same sentence with a flag, and it always wins: give the whole team the branch, deny one person, done. Grants can expire on a date (a three-month contractor). Every grant records who gave it, when, why, and who revoked it. Some access is not granted at all but **derived** from a business fact the host already holds ("the assigned representative can read that customer's orders"), and disappears when the fact changes.

The single question "may Hossam edit this order?" and the list question "which orders may Hossam edit?" are answered from the same definition, inside the database query itself, so a list screen can never show a row the single check would refuse. Every record comes back with its **capabilities**: the actions this person may take on it, and what each still needs (a re-authentication, say). Buttons are drawn from that, so a button is never a guess. A refused request for a specific record answers "not found", indistinguishable from a real not-found, so nobody can learn a record exists by being refused; the reason is available to support through an audit lookup. Revoking a grant, editing a role or moving a record takes effect on the very next request.

System administration is a permission like any other, never implied by seniority, and can only be conferred by someone who holds it.

### 7.3 Invitations, domain lock, offboarding

Invitations were covered in 3.2. The **domain lock** is optional per organisation: the organisation lists its domains, each proven once by placing a record in the domain's public name registry (DNS, the system that maps names like example.com to servers; only the domain's owner can add records there), and while the lock is on, only addresses in those domains can sign in as members. Whether the proof is re-checked later is an open point; see section 11. Personal addresses on a member's account stay for notices and recovery.

When Hossam leaves the company, the offboarding checklist runs in order: end his sessions, suspend the account (reversible), end the membership, transfer any grants only he held, review anything he approved, revoke his mail app password, and verify the mail server actually disabled his mailbox by reading the daily reconciliation report rather than assuming. The corporate address stops working as a sign-in and the mailbox is disabled. His personal email becomes his primary address automatically, the domain lock no longer applies to him, and the account carries on as an ordinary account without a pause. The corporate address is retired and can be given to the next hire. Two days later, a check that nothing survived. If the departure is hostile, everything happens in one sitting before he is told, and any credential he may have seen is changed.

---

## 8. What the business decides

### 8.1 Policy switches

Some settings are **per organisation** (inheriting the system's where not set); the rest are system-wide. All but one are changed at runtime from the management app, audited, no restart. Tightening is free; loosening asks for a strong re-authentication and a reason. The governing legal language is the exception: it is set when the system is installed and changing it needs a restart, because it changes which text is legally binding.

| Setting | Default | What it changes |
|---|---|---|
| Adults only | On | Whether step 1 refuses under-18s or only records an age group |
| Keep date of birth | Off (keep only the answer) | Whether the date itself is stored |
| Phone number | Required | Whether a mobile number is needed to register |
| Emails and phones per account | Several | Whether extra addresses can be added |
| Sign-in methods offered | Passkey, password, Google, Apple | Email link, SMS link and SMS second step are off until switched on |
| Username | Off | Whether a public username exists at all |
| Legal name | Off | Whether it is asked for (optional or required) |
| Profile photo | Staff only | Where photos are allowed |
| Required sign-in strength (per organisation) | Basic for customers, strong for staff | What a session must reach to be allowed in |
| Credential redundancy (per organisation) | Enforced for staff, offered to customers | Whether a second credential is required when the first one lives on a single device |
| Grace when a requirement is raised | None | How long existing accounts get to comply before being stopped at sign-in |
| Gates (per organisation) | Customers: their own reachable level within 15 min; staff: strong and phishing-proof within 15 min | What sensitive actions require |
| New-device check | On for single-factor accounts | The emailed code on an unrecognised browser |
| Domain lock (per organisation) | Off | Which domains members may sign in with |
| Extra password blocklists | Off | Whether dictionary words or the person's own name are refused rather than warned about |
| Governing legal language | Must be set at installation | Which language of each legal document prevails. Restart to change. |

### 8.2 Limits on texts and emails

Texts cost money from a prepaid balance, so sending is governed by **restrictions**: named rules, each counted against something (a phone number, an account, a source address, everything, or any key the host supplies), each with one or more "at most N in M hours" buckets, all checked on every send. The defaults: 3 texts per number, each regaining a day later (so a number gets one text back per day), 10 per source address per hour; 5 emails per address per hour. Every kind of message draws from the same pool. SMS sign-in and the SMS second step are off by default for two reasons: the standard classes phone-based sign-in as restricted (numbers can be hijacked), and with the default pool a person could spend three texts on three sign-ins. A business that turns them on raises the pool. Restrictions are edited at runtime; support can grant one extra send to a specific number, audited with a reason. Security notices to an existing account holder sit outside the pool, so nobody can silence the owner by draining it.

### 8.3 Alerts

Every condition that needs a human is on a list with a severity. **High** goes by email and text: emergency credential used, a protected setting changed, alert destinations changed, sustained sign-in failures, a cluster of recovery requests, a privacy deadline reached, a failed restore test, no emergency credential in existence. **Normal** goes by email: text balance draining, a background job failing, the holiday list running out, a licence approaching expiry, and the like. Changing where alerts go tells the previous destinations, and the last destination of a channel can never be removed.

---

## 9. Behind the curtain, briefly

**Four apps, one cookie each.** The business has a storefront, a management app for staff, an authentication app where all sign-ins happen, and an account app. Each runs behind a thin server layer that holds Hossam's session; his browser holds only an opaque cookie that decodes to nothing. No token, permission or role ever reaches the browser, so nothing can be stolen from it. Moving between the apps is silent. Signing out of one signs out of all.

**Everyone has their own key.** Every personal field, in Janus's tables and in the host's, is encrypted under Hossam's own key, itself locked under a master key held in a separate vault. Erasure is the destruction of his key. Email addresses and phone numbers are also stored as keyed fingerprints so an address can be looked up without being readable.

**Everything is audited.** Every sign-in, change, grant, approval and administrative action is recorded, append-only, with who acted and why, holding codes rather than personal details, kept 7 years for security and financial events and 90 days for routine access.

**Outside services, and what each is trusted with.** The mail server (Stalwart) hosts staff mailboxes and sends every message; it trusts Janus for sign-in and is pushed every account change, with a daily comparison that reports drift. The payment provider sees the amount and returns a reference; card numbers never touch the system. The courier receives a generic package description, the recipient's name, phone and address, and a cash-on-delivery amount, never product names. The SMS gateway receives numbers and codes. Every message these services send back is treated as a hint to go and check, never as the truth.

**Deployments.** A push to the main branch builds, tests, migrates the database and deploys, with no manual step. Configuration lives in the database and is changed from the management app; only the database connection and the vault credential are set at deployment.

---

## 10. When things go wrong

### 10.1 A customer turns out to be a minor

Only on a credible indication (they say so, a guardian writes in, something specific in support), never a guess from a name or a purchase. One operation does phase one at once: the account is suspended, every session ends, undispatched orders are cancelled through the host, and the reason is recorded. Seven days later, automatically, the account is erased. Inside those seven days, the person holding the takedown permission can reverse it from the account's admin page (the account returns, the cancelled orders do not). Nobody asks for ID documents. The breach clock (10.3) starts at the moment of the credible indication.

### 10.2 The emergency envelope

Hossam, as owner, keeps a sealed, tamper-evident envelope with five printed items: a single-use emergency sign-in code, the three keys needed to read a restored database, and the credentials needed to redeploy the system on a new server. The code is for the day no administrator can reach the system (including the sole administrator locking themselves out): open the envelope, go to the printed address, enter the code, and for four hours act with full administration rights while every alert channel tells everyone. Before finishing, generate and seal a replacement. Once a year the envelope is opened, everything in it rotated, a restore from the envelope's contents alone is rehearsed by hand (that is what keeping copies of keys in a sealed envelope, called escrow, is for), and it is resealed. Until an emergency code exists, every administrator sees a non-dismissable warning.

### 10.3 A data breach

Detection starts two clocks: 72 hours to notify the regulator and 3 days to notify the affected people, both counted from detection and not deferrable while investigating. The audit trail answers "who was affected" by a query, and breach notices cannot be suppressed by any preference.

### 10.4 Losing the server

The database continuously ships its change log, so at most seconds of data are lost and a restore can land on any moment. Getting back online takes 4 to 8 hours (the business asked for 2 to 3; that would need a second server, declined on cost). A restore is followed by reconciliation with the payment provider, the courier and the mail server, because the outside world did not roll back with the database, and a restore that loses personal data starts the breach clocks. A restore is rehearsed automatically every quarter into a throwaway database, with a canary account that must decrypt and sign in, alerting on any failure.

The accepted gap, stated plainly: until the hosting tier is upgraded, backups sit on the same server as the database. Losing the server means losing everything, and the data by then includes health information. This is recorded as accepted under time pressure, with no date, and is the biggest single risk on the register.

### 10.5 Other accepted risks, in one line each

Mail app passwords bypass passkeys (mail programs offer nothing better; they work only for mail, are revocable, and never sign in to anything else). One person approves recoveries (written reason, separate-channel notice, anomaly alerts). One person holds all technical knowledge (the envelope is with the owner, not the developer). Age is self-declared (the alternative is holding identity documents). A stolen trusted device skips the second step for up to 30 days. Google or Apple sign-in skips the customer's own second step (bounded by the gates on sensitive actions). The courier can infer that a medical company shipped something.

---

## 11. Points settled last

Four behaviours the earlier decisions had left open. They were settled as written here and are now part of the specifications (decision D-146, chapter 20).

| Point | Answer |
|---|---|
| A former staff member's account after offboarding | Every staff invitation names a personal email, verified by the invitation link itself, and it stays on the account as a second email that cannot sign in while the domain lock applies. When membership ends it becomes primary automatically and the account carries on; the corporate address is retired for the next hire. (Your proposal, D-148.) |
| An existing account accepting an invitation | Hossam signs in and goes straight to the membership step; the invitation's corporate address is added to his account; the organisation's credential policy is enforced before membership attaches. |
| Whether a domain-lock proof is re-checked | Re-checked automatically on a schedule; a failure raises an alert and never revokes anything by itself; removing a domain from the list stops new sign-ins with those addresses and raises an alert. |
| What happens to a username after erasure | Held for the same period as the consent records, then released, so an erased person cannot be impersonated straight away. |

## Appendix A. Where each section lives in the specifications

| Section | Chapters |
|---|---|
| 1 What Janus is | 00 Overview, 07 Library contract |
| 2 The account | 01 Identity, 20 Registration and account |
| 3 Getting an account | 20 Registration and account, 18 Frontend integration, 05 Integrations (mail) |
| 4 Signing in | 02 Authentication, 17 BFF |
| 5 Staying safe | 02 Authentication |
| 6 Own controls and privacy | 01 Identity, 04 Privacy, 09 API contract |
| 7 Organisation side | 01 Identity, 03 Authorization, 16 Offboarding |
| 8 Configuration | 10 Reference, 06 Operations |
| 9 Behind the curtain | 17 BFF, 19 Infrastructure, 05 Integrations, 06 Operations |
| 10 When things go wrong | 14 Takedown, 11 Runbook, 12 Disaster recovery, 13 Risk register, 15 Threat model |

## Appendix B. Terms

| Term | Meaning here |
|---|---|
| Host | The company's own application that imports Janus |
| Passkey | A sign-in credential kept on a device or password manager, unlocked by fingerprint, face or PIN; cannot be phished |
| Second step | Something presented after the password: authenticator app, security key, SMS code or recovery code |
| Gate | The check before a sensitive action: how strong, how recent, and whether phishing-proof the sign-in must be |
| Reachable level | The strongest sign-in an account could make with the credentials it currently holds |
| Security-notice set | The addresses that receive security notices and can recover the account: primary, primary plus backup, or all confirmed |
| Restriction | A rule limiting how many messages may be sent to, or from, something in a period |
| Grant | One permission: someone has a role on something |
| Capability | An action the server says a person may take on a specific record |
| Erasure | Destroying a person's encryption key so every personal field becomes unreadable, while records survive anonymous |
| Governing language | The language of a legal document that prevails when translations differ |
| Domain lock | An organisation's rule that members sign in only with addresses in its verified domains |
| App password | A separate password for mail programs that cannot use passkeys; mail-only, revocable |
| Emergency envelope | The owner's sealed envelope with the break-glass code and the recovery keys |
| BFF | The thin server layer behind each app that holds the session so the browser holds only a cookie |
