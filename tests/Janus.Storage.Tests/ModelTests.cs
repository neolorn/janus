using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What the one context maps (CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// The model is built from the design-time factory, which connects to nothing, so this
/// reads the mapping and not a database.
/// </remarks>
[Trait("kind", "unit")]
public sealed class ModelTests
{
    /// <summary>
    /// CONV-DESIGN-003 AC4: no domain entity type appears in the model. What is mapped
    /// is a persistence record, declared in this project beside its configuration, so
    /// an aggregate can never be loaded, tracked or written by the context itself.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC4_NoDomainEntityTypeAppearsInTheModel()
    {
        IEnumerable<Type> mapped = Model().GetEntityTypes().Select(entity => entity.ClrType);

        Assert.NotEmpty(mapped);
        Assert.All(mapped, type => Assert.Equal(typeof(JanusDbContext).Assembly, type.Assembly));
    }

    /// <summary>
    /// CONV-DESIGN-003: every table the library owns is mapped in the schema the library
    /// owns, so nothing of the host's is ever read or written through this context.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema() =>
        Assert.All(
            Model().GetEntityTypes(),
            entity => Assert.Equal(JanusDbContext.Schema, entity.GetSchema()));

    /// <summary>
    /// IDN-ORG-002 AC1: nothing in the schema says which kind of person a row is
    /// about. Staff are the members of the administrative organization, so a flag
    /// separating them from customers would be a second answer to a question the
    /// membership already answers.
    /// </summary>
    [Fact]
    public void IDN_ORG_002_AC1_NoColumnDistinguishesStaffFromCustomers() =>
        RefuseColumnsNamedAfter(["staff", "customer", "employee", "internal", "external"]);

    /// <summary>
    /// REG-ACCT-001 AC2: every column the library maps is a field the item's table
    /// names, or one the chapter that table points to requires. A column that belongs
    /// to neither is a field the account grew without a rule for who fills it, so the
    /// set is written out here and compared whole.
    /// </summary>
    [Fact]
    public void REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames()
    {
        string[] expected =
        [
            // Preferences: language and time zone in plain, the declared values under
            // the key, as the table's Key column says of each.
            "account_preferences.enc_values",
            "account_preferences.language",
            "account_preferences.subject",
            "account_preferences.time_zone",

            // Standing: the state and the two windows `01` section 4 gives it, with
            // what the terms step wrote down of the age answer, the affirmation
            // derived from it and the versions accepted (REG-PROF-002, REG-SESS-007).
            "accounts.adult_affirmed",
            "accounts.age_group",
            "accounts.answered_age_at",
            "accounts.created_at",
            "accounts.deleting_by",
            "accounts.deleting_since",
            "accounts.notice_version",
            "accounts.state",
            "accounts.subject",
            "accounts.suspended_by",
            "accounts.terms_version",

            // Not an account field: the alert ledger of OPS-ALERT-002, holding one row
            // per condition told and when it last went out.
            "alerts.at",
            "alerts.key",

            // Authorization: the ancestry closure of AUTHZ-INHERIT-002, which is what a
            // permission query joins instead of walking the tree.
            "ancestry.ancestor_id",
            "ancestry.ancestor_type",
            "ancestry.depth",
            "ancestry.organization",
            "ancestry.resource_id",
            "ancestry.resource_type",

            // Standing: the event record of IDN-AUD-001, with the attribute column
            // PRIV-RET-002 puts an event's personal field in.
            "audit_records.acting_subject",
            "audit_records.action",
            "audit_records.category",
            "audit_records.details",
            "audit_records.effective_subject",
            "audit_records.enc_details",
            "audit_records.id",
            "audit_records.occurred_at",
            "audit_records.organization",

            // Credentials: the enrolled authenticator of AUTH-FACT-001, the shared secret
            // of AUTH-FACT-006 under the key, and the WebAuthn columns AUTH-FACT-011 and
            // AUTH-FACT-012 read.
            "authenticators.added_at",
            "authenticators.algorithm",
            "authenticators.backup_eligible",
            "authenticators.backup_state",
            "authenticators.confirmed",
            "authenticators.counter",
            "authenticators.credential_id",
            "authenticators.factor",
            "authenticators.id",
            "authenticators.invalidates_at",
            "authenticators.is_preferred",
            "authenticators.label",
            "authenticators.last_used_at",
            "authenticators.public_key",
            "authenticators.relying_party",
            "authenticators.state",
            "authenticators.subject",
            "authenticators.totp_consumed_step",
            "authenticators.totp_secret",

            // Not an account field: the inbound callbacks counted per source
            // (INT-GEN-003, BFF-MACH-003), the source held by its hash.
            "callbacks.at",
            "callbacks.id",
            "callbacks.rejected",
            "callbacks.source",

            // Not an account field: what the subject consented to, against which version
            // of the notice and where they said it (PRIV-CONS-001). Nothing here is
            // deleted: a withdrawal is a timestamp, because the record is the evidence.
            "consents.granted_at",
            "consents.kind",
            "consents.mechanism",
            "consents.notice_version",
            "consents.purpose",
            "consents.subject",
            "consents.superseded_at",
            "consents.withdrawn_at",

            // Credentials: the browser an account knows (AUTH-FACT-015, AUTH-FACT-016),
            // held by the fingerprint of its token and never by the token.
            "devices.consecutive_failures",
            "devices.created_at",
            "devices.expires_at",
            "devices.id",
            "devices.kind",
            "devices.label",
            "devices.last_used_at",
            "devices.revoked",
            "devices.subject",
            "devices.token_fingerprint",

            // Standing: the host-side progress of an erasure (IDN-LIFE-003b).
            "erasures.attempts",
            "erasures.reason",
            "erasures.requested_at",
            "erasures.status",
            "erasures.subject",

            // Authorization: the counter of AUTHZ-CACHE-001, raised in the transaction of
            // the change that orphans an entry.
            "grant_versions.subject",
            "grant_versions.version",

            // Authorization: the one sentence of AUTHZ-GRANT-001, with the audit fields
            // AUTHZ-GRANT-003 requires of a grant and of its revocation.
            "grants.deny",
            "grants.expires_at",
            "grants.granted_at",
            "grants.granted_by",
            "grants.id",
            "grants.kind",
            "grants.organization",
            "grants.reason",
            "grants.resource_id",
            "grants.resource_type",
            "grants.revocation_reason",
            "grants.revoked_at",
            "grants.revoked_by",
            "grants.role",
            "grants.subject_id",
            "grants.subject_type",

            // Authorization: the group closure of AUTHZ-GROUP-001, read once per request
            // rather than walked per check.
            "group_closure.depth",
            "group_closure.group_id",
            "group_closure.member_id",
            "group_closure.member_type",

            // Authorization: the memberships of AUTHZ-GROUP-001, which the closure is
            // rebuilt from.
            "group_members.group_id",
            "group_members.member_id",
            "group_members.member_type",

            // Authorization: the group of AUTHZ-GROUP-001, which holds grants on behalf
            // of its members.
            "groups.id",
            "groups.name",
            "groups.organization",

            // Identifiers: the backup setting of `10` section 5.17.
            "identifier_backup_settings.kind",
            "identifier_backup_settings.named",
            "identifier_backup_settings.rule",
            "identifier_backup_settings.subject",

            // Identifiers: what a removal keeps for the length of its undo window,
            // the row as it stood with the fingerprint the undo link answers to
            // (REG-IDENT-006).
            "identifier_removals.added_at",
            "identifier_removals.enc_canonical",
            "identifier_removals.enc_entered",
            "identifier_removals.expires_at",
            "identifier_removals.fingerprint",
            "identifier_removals.identifier_id",
            "identifier_removals.is_locked",
            "identifier_removals.kind",
            "identifier_removals.removed_at",
            "identifier_removals.subject",
            "identifier_removals.undo_fingerprint",
            "identifier_removals.verified_at",

            // Identifiers: one staged identifier waiting to be proved, the browser
            // that staged it, and what a replace has to put back (REG-IDENT-004,
            // REG-IDENT-007, REG-SESS-003).
            "identifier_verifications.browser",
            "identifier_verifications.enc_staged",
            "identifier_verifications.identifier_id",
            "identifier_verifications.is_replacement",
            "identifier_verifications.link",
            "identifier_verifications.old_confirmed_at",
            "identifier_verifications.old_link",
            "identifier_verifications.old_must_confirm",
            "identifier_verifications.staged_at",
            "identifier_verifications.subject",

            // Identifiers: the two forms and the fingerprint of IDN-ACCT-004 and
            // PRIV-RIGHT-005c, with the roles REG-IDENT-002 gives a row.
            "identifiers.added_at",
            "identifiers.canonicalisation_version",
            "identifiers.enc_canonical",
            "identifiers.enc_entered",
            "identifiers.fingerprint",
            "identifiers.identifier_id",
            "identifiers.is_locked",
            "identifiers.is_primary",
            "identifiers.kind",
            "identifiers.subject",
            "identifiers.verified_at",

            // Not an account field: the creation ceremony one account has open, and
            // the value AUTH-FACT-014 has the authenticator sign over.
            "key_ceremonies.challenge",
            "key_ceremonies.expires_at",
            "key_ceremonies.issued_at",
            "key_ceremonies.kind",
            "key_ceremonies.subject",
            "key_ceremonies.upgrading",

            // Not an account field: the legal documents the deployment publishes, each
            // version binding in the one language it names (PRIV-CONS-005,
            // PRIV-CONS-006).
            "legal_document_translations.document",
            "legal_document_translations.language",
            "legal_document_translations.translated_text",
            "legal_document_translations.version",
            "legal_document_versions.document",
            "legal_document_versions.governing_language",
            "legal_document_versions.governing_text",
            "legal_document_versions.published_at",
            "legal_document_versions.version",

            // Not an account field: a credential its holder reported lost, the window
            // AUTH-RECOV-007 invalidates it at the end of, and what the notices across
            // that window have reached.
            "loss_reports.any_delivered",
            "loss_reports.credential",
            "loss_reports.enc_cancel",
            "loss_reports.held_at",
            "loss_reports.invalidates_at",
            "loss_reports.notified_at",
            "loss_reports.reported_at",
            "loss_reports.subject",

            // Standing: the membership record of IDN-MEM-001, with its own beginning
            // and end.
            "memberships.created_at",
            "memberships.ended_at",
            "memberships.id",
            "memberships.organization",
            "memberships.subject",

            // Not an account field: the tellings of AUTH-ABUSE-003 that no account holds
            // an address, counted against the hash of the address.
            "nonexistence_notices.at",
            "nonexistence_notices.destination",
            "nonexistence_notices.id",

            // Not an account field: the purposes on an objectable basis the subject has
            // objected to (PRIV-RIGHT-001a).
            "objections.mechanism",
            "objections.notice_version",
            "objections.purpose",
            "objections.recorded_at",
            "objections.subject",
            "objections.withdrawn_at",

            // Not an account field: the clients the deployment registered with the
            // provider, the codes waiting to be exchanged and the refresh tokens of
            // AUTH-OIDC-001, AUTH-OIDC-003 and AUTH-SESS-012.
            "oidc_clients.client_id",
            "oidc_clients.kind",
            "oidc_clients.name",
            "oidc_clients.redirect",
            "oidc_clients.scopes",
            "oidc_clients.secret",
            "oidc_codes.challenge",
            "oidc_codes.challenge_method",
            "oidc_codes.client_id",
            "oidc_codes.expires_at",
            "oidc_codes.fingerprint",
            "oidc_codes.issued_at",
            "oidc_codes.nonce",
            "oidc_codes.redirect",
            "oidc_codes.scope",
            "oidc_codes.session",
            "oidc_codes.spent_at",
            "oidc_codes.subject",
            "oidc_refresh_tokens.client_id",
            "oidc_refresh_tokens.consumed_at",
            "oidc_refresh_tokens.expires_at",
            "oidc_refresh_tokens.family",
            "oidc_refresh_tokens.fingerprint",
            "oidc_refresh_tokens.issued_at",
            "oidc_refresh_tokens.scope",
            "oidc_refresh_tokens.session",
            "oidc_refresh_tokens.subject",

            // The organization of IDN-ORG-001, with the mark IDN-ORG-004 reads and
            // the deletion window of IDN-ORG-003.
            "organizations.administrative",
            "organizations.created_at",
            "organizations.deletion_requested_at",
            "organizations.erased_at",
            "organizations.id",
            "organizations.name",

            // Not an account field: one fact about a subject the host has its own half
            // of, and each subscriber's confirmation of it (IDN-LIFE-003a).
            "outbox.attempts",
            "outbox.id",
            "outbox.kind",
            "outbox.next_attempt_at",
            "outbox.raised_at",
            "outbox.reason",
            "outbox.restricted",
            "outbox.status",
            "outbox.subject",
            "outbox_confirmations.confirmed_at",
            "outbox_confirmations.delivery",
            "outbox_confirmations.subscriber",

            // Credentials: the password hash of AUTH-PASS-007 and the floor flag
            // AUTH-PASS-001a says cannot be recomputed from it.
            "passwords.change_required",
            "passwords.hash",
            "passwords.meets_single_factor_floor",
            "passwords.set_at",
            "passwords.subject",

            // Not an account field: a requirement a scope's policy raised and when the
            // run-up to it began, one row per scope per field, which a sign-in that does
            // not yet meet it is told of (AUTH-FACT-017).
            "policy_raises.field",
            "policy_raises.id",
            "policy_raises.organization",
            "policy_raises.raised_at",
            "policy_raises.value",

            // Not an account field: what a browser carries before it holds a session,
            // keyed as the session table is and carrying the registration session in
            // flight (BFF-CSRF-005a, BFF-CSRF-005b).
            "preauthentication_sessions.created_at",
            "preauthentication_sessions.csrf_fingerprint",
            "preauthentication_sessions.enrolment",
            "preauthentication_sessions.expires_at",
            "preauthentication_sessions.fingerprint",
            "preauthentication_sessions.registration",

            // Not an account field: the data subject requests of PRIV-RIGHT-001 with
            // the three instants the working-day clock gives each one (PRIV-RIGHT-002).
            "privacy_requests.channel",
            "privacy_requests.created_at",
            "privacy_requests.decided_at",
            "privacy_requests.decision_due",
            "privacy_requests.decision_reason",
            "privacy_requests.detail",
            "privacy_requests.escalate_at",
            "privacy_requests.escalated_at",
            "privacy_requests.id",
            "privacy_requests.identity_confirmation",
            "privacy_requests.received_at",
            "privacy_requests.status",
            "privacy_requests.subject",
            "privacy_requests.type",
            "privacy_requests.warn_at",
            "privacy_requests.warned_at",

            // Profile: the photo, in a table of its own (IDN-ATTR-003).
            "profile_photos.enc_image",
            "profile_photos.subject",
            "profile_photos.updated_at",

            // Profile: the three declared fields of REG-PROF-001, all under the key.
            "profiles.enc_date_of_birth",
            "profiles.enc_display_name",
            "profiles.enc_legal_name",
            "profiles.subject",

            // Not an account field: one approver standing behind one re-enrolment, on
            // the channel they confirmed the person on, which is held under the
            // account's key (AUTH-RECOV-002, AUTH-RECOV-003).
            "recovery_approvals.approved_at",
            "recovery_approvals.approver",
            "recovery_approvals.enc_channel",
            "recovery_approvals.spent_at",
            "recovery_approvals.subject",

            // Credentials: the recovery-code set of AUTH-FACT-008, with the instants the
            // account shows and the reminder reads.
            "recovery_code_sets.exported_at",
            "recovery_code_sets.generated_at",
            "recovery_code_sets.reminded_at",
            "recovery_code_sets.subject",
            "recovery_code_sets.viewed_at",

            // Credentials: the codes of that set, hashed as passwords are, in the order
            // they were drawn.
            "recovery_codes.hash",
            "recovery_codes.ordinal",
            "recovery_codes.subject",
            "recovery_codes.used_at",

            // Not an account field: a recovery link, answered to by what it fingerprints
            // to, and, once spent, the enrolment session it opened (AUTH-RECOV-002).
            "recovery_links.approver",
            "recovery_links.expires_at",
            "recovery_links.issued_at",
            "recovery_links.mailbox_lost",
            "recovery_links.purpose",
            "recovery_links.session",
            "recovery_links.spent_at",
            "recovery_links.subject",
            "recovery_links.token",

            // Not an account field: the link a registration session sent, answered to
            // by what it fingerprints to (REG-SESS-003).
            "registration_links.fingerprint",
            "registration_links.session",

            // Not an account field: the registration session of REG-SESS-001, which
            // reserves nothing: everything it stages is one blob under its own key,
            // discarded whole when it ends.
            "registration_sessions.enc_session",
            "registration_sessions.expires_at",
            "registration_sessions.id",
            "registration_sessions.key_version",
            "registration_sessions.provisional_subject",
            "registration_sessions.wrapped_key",

            // Not an account field: the registration sessions of AUTH-ABUSE-008, counted
            // against the hash of the source they were started from.
            "registration_sources.at",
            "registration_sources.id",
            "registration_sources.source",

            // Authorization: the host's records as AUTHZ-INHERIT-001 registers them, and
            // the one containing each.
            "resources.contained_in_id",
            "resources.contained_in_type",
            "resources.organization",
            "resources.resource_id",
            "resources.resource_type",

            // Authorization: what a role allows (AUTHZ-GRANT-004), read live so that
            // editing it takes effect at once.
            "role_permissions.permission",
            "role_permissions.role",
            "roles.name",

            // Not an account field: the sending counters of AUTH-ABUSE-004, an HMAC of the
            // restriction key with the times counted against it, and the credit support
            // kept apart from them.
            "send_counters.key",
            "send_counters.sent_at",
            "send_counters.settles_at",
            "send_grants.credit",
            "send_grants.key",

            // Not an account field: the message a transport took (AUTH-ABUSE-004,
            // INT-SMS-005), held by the hash of its correlation reference so that a
            // delivery report can take its counts back out.
            "sends.counted",
            "sends.reference",
            "sends.sent_at",
            "sends.settles_at",

            // Sessions: the spine of AUTH-SESS-001, what it reached (AUTH-SESS-002), the
            // fingerprint of its secret (AUTH-SESS-003), and where it was used from with
            // the place under the key (AUTH-SESS-013).
            "sessions.absolute_expiry",
            "sessions.attained",
            "sessions.attained_at",
            "sessions.created_at",
            "sessions.csrf_fingerprint",
            "sessions.ended_at",
            "sessions.id",
            "sessions.idle_expiry",
            "sessions.last_seen_at",
            "sessions.last_seen_browser",
            "sessions.last_seen_os",
            "sessions.last_seen_place",
            "sessions.origin_browser",
            "sessions.origin_os",
            "sessions.origin_place",
            "sessions.phishing_resistant",
            "sessions.phishing_resistant_at",
            "sessions.satisfies_every_gate",
            "sessions.secret_fingerprint",
            "sessions.spine",
            "sessions.subject",
            "sessions.type",

            // Not an account field: the runtime configuration of OPS-CFG-008.
            "settings.key",
            "settings.value",

            // Not an account field: a sign-in in flight, keyed by what the caller's handle
            // hashes to and carrying what it has presented so far (AUTH-FACT-001).
            "signin_challenges.created_at",
            "signin_challenges.device_attempts",
            "signin_challenges.device_code",
            "signin_challenges.expires_at",
            "signin_challenges.handle",
            "signin_challenges.presented",
            "signin_challenges.subject",
            "signin_challenges.webauthn",

            // Not an account field: a link or a code the library sent for a sign-in,
            // one per account per catalogue entry, spent on presentation
            // (AUTH-FACT-016, REG-SESS-003).
            "signin_links.browser",
            "signin_links.enc_code",
            "signin_links.expires_at",
            "signin_links.factor",
            "signin_links.issued_at",
            "signin_links.subject",
            "signin_links.token",
            "signin_links.wrong_attempts",

            // Not an account field: the keys the provider signs tokens with, whose
            // private material is wrapped under the key-encryption key (AUTH-KEY-001,
            // AUTH-KEY-002).
            "signing_keys.algorithm",
            "signing_keys.created_at",
            "signing_keys.key_id",
            "signing_keys.key_version",
            "signing_keys.private_key",
            "signing_keys.public_key",
            "signing_keys.retires_at",
            "signing_keys.superseded_at",

            // Not an account field: what the gateway last said its prepaid account stood
            // at (INT-SMS-004, AUTH-ABUSE-006).
            "sms_balance_readings.balance",
            "sms_balance_readings.read_at",

            // Not an account field: the wrapped key of PRIV-RIGHT-005a, which every
            // column marked Key is written under.
            "subject_keys.format_marker",
            "subject_keys.key_version",
            "subject_keys.subject",
            "subject_keys.wrapped_key",

            // Not an account field: the failures of AUTH-ABUSE-001 by scope, each key an
            // HMAC, so an identifier no account holds leaves no readable trace of having
            // been typed.
            "throttle_counters.at",
            "throttle_counters.failures",
            "throttle_counters.key",
            "throttle_counters.scope",

            // Not an account field: the username an erasure left held for as long as
            // `retention.consent` asks, so that nobody takes it in the meantime
            // (REG-IDENT-009, PRIV-RET-002).
            "username_holds.fingerprint",
            "username_holds.held_from",
            "username_holds.releases_at",
        ];

        Assert.Equal(expected, Columns().OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// IDN-ATTR-005 AC1: no table of the library's holds an address. Where the host
    /// keeps one it is the host's own, and nothing here has a column to put it in.
    /// </summary>
    [Fact]
    public void IDN_ATTR_005_AC1_NoLibraryTableHoldsAnAddress() =>
        RefuseColumnsNamedAfter(["address", "street", "city", "postcode", "district"]);

    /// <summary>
    /// IDN-ATTR-006 AC1: no field holds a latitude or a longitude. A location resolves
    /// to an area and the coordinates are discarded before anything is written.
    /// </summary>
    [Fact]
    public void IDN_ATTR_006_AC1_NoSchemaFieldHoldsCoordinates() =>
        RefuseColumnsNamedAfter(["latitude", "longitude", "coordinate"]);

    /// <summary>
    /// IDN-ATTR-007 AC1: the profile is the display name, the legal name, the date of
    /// birth and the photo. A fifth field would be one nothing states a rule for.
    /// </summary>
    [Fact]
    public void IDN_ATTR_007_AC1_TheProfileIsTheFourFieldsAndNothingElse()
    {
        string[] expected =
        [
            "profile_photos.enc_image",
            "profiles.enc_date_of_birth",
            "profiles.enc_display_name",
            "profiles.enc_legal_name",
        ];

        IEnumerable<string> fields = Columns()
            .Where(column => column.Contains(".enc_", StringComparison.Ordinal))
            .Where(column => column.StartsWith("profile", StringComparison.Ordinal));

        Assert.Equal(expected, fields.OrderBy(name => name, StringComparer.Ordinal));
    }

    private static void RefuseColumnsNamedAfter(string[] forbidden) =>
        Assert.All(Columns(), column => Assert.DoesNotContain(
            forbidden,
            word => column.Contains(word, StringComparison.OrdinalIgnoreCase)));

    private static List<string> Columns() =>
        Model()
            .GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Select(property => entity.GetTableName() + "." + property.GetColumnName()))
            .ToList();

    private static IModel Model()
    {
        using JanusDbContext context = new DesignTimeContextFactory().CreateDbContext([]);

        return context.Model;
    }
}
