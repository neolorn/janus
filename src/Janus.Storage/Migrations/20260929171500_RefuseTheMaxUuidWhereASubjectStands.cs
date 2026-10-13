using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RefuseTheMaxUuidWhereASubjectStands : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-RIGHT-005a AC18, D-174: every column of a library table that can hold a
        // subject identifier in any row refuses the max UUID, under which the
        // deployment's data key is held. The audit trail is partitioned and outside the
        // model, so its identities take the check here, and its partitions inherit it.
        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records
                ADD CONSTRAINT ck_audit_records_acting_subject_not_max_uuid
                CHECK (acting_subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff');
            ALTER TABLE identity.audit_records
                ADD CONSTRAINT ck_audit_records_effective_subject_not_max_uuid
                CHECK (effective_subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff');
            """);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_links_subject_not_max_uuid",
            schema: "identity",
            table: "signin_links",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_subject_not_max_uuid",
            schema: "identity",
            table: "signin_challenges",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_sessions_subject_not_max_uuid",
            schema: "identity",
            table: "sessions",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_send_outbox_subject_not_max_uuid",
            schema: "identity",
            table: "send_outbox",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_resources_subject_not_max_uuid",
            schema: "identity",
            table: "resources",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_registration_sessions_provisional_subject_not_max_uuid",
            schema: "identity",
            table: "registration_sessions",
            sql: "provisional_subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_links_approver_not_max_uuid",
            schema: "identity",
            table: "recovery_links",
            sql: "approver <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_links_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_links",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_codes_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_codes",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_code_sets_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_code_sets",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_approvals_approver_not_max_uuid",
            schema: "identity",
            table: "recovery_approvals",
            sql: "approver <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_recovery_approvals_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_approvals",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_read_volume_actor_not_max_uuid",
            schema: "identity",
            table: "read_volume",
            sql: "actor <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_read_baselines_actor_not_max_uuid",
            schema: "identity",
            table: "read_baselines",
            sql: "actor <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_profiles_subject_not_max_uuid",
            schema: "identity",
            table: "profiles",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_profile_photos_subject_not_max_uuid",
            schema: "identity",
            table: "profile_photos",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_privacy_requests_subject_not_max_uuid",
            schema: "identity",
            table: "privacy_requests",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_privacy_exports_subject_not_max_uuid",
            schema: "identity",
            table: "privacy_exports",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_passwords_subject_not_max_uuid",
            schema: "identity",
            table: "passwords",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_subject_not_max_uuid",
            schema: "identity",
            table: "outbox",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_tokens_subject_not_max_uuid",
            schema: "identity",
            table: "oidc_tokens",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_authorizations_subject_not_max_uuid",
            schema: "identity",
            table: "oidc_authorizations",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_objections_subject_not_max_uuid",
            schema: "identity",
            table: "objections",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_memberships_subject_not_max_uuid",
            schema: "identity",
            table: "memberships",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_maintenance_log_actor_not_max_uuid",
            schema: "identity",
            table: "maintenance_log",
            sql: "actor <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_mailboxes_holder_not_max_uuid",
            schema: "identity",
            table: "mailboxes",
            sql: "holder <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_loss_reports_subject_not_max_uuid",
            schema: "identity",
            table: "loss_reports",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_lifecycle_links_subject_not_max_uuid",
            schema: "identity",
            table: "lifecycle_links",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_key_ceremonies_subject_not_max_uuid",
            schema: "identity",
            table: "key_ceremonies",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_invitee_not_max_uuid",
            schema: "identity",
            table: "invitations",
            sql: "invitee <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_invitations_inviter_not_max_uuid",
            schema: "identity",
            table: "invitations",
            sql: "inviter <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_identifiers_subject_not_max_uuid",
            schema: "identity",
            table: "identifiers",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_identifier_verifications_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_verifications",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_identifier_removals_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_removals",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_identifier_backup_settings_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_backup_settings",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_group_members_member_id_not_max_uuid",
            schema: "identity",
            table: "group_members",
            sql: "member_id <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_group_closure_member_id_not_max_uuid",
            schema: "identity",
            table: "group_closure",
            sql: "member_id <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_grants_granted_by_not_max_uuid",
            schema: "identity",
            table: "grants",
            sql: "granted_by <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_grants_revoked_by_not_max_uuid",
            schema: "identity",
            table: "grants",
            sql: "revoked_by <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_grants_subject_id_not_max_uuid",
            schema: "identity",
            table: "grants",
            sql: "subject_id <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_grant_versions_subject_not_max_uuid",
            schema: "identity",
            table: "grant_versions",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_erasures_subject_not_max_uuid",
            schema: "identity",
            table: "erasures",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_devices_subject_not_max_uuid",
            schema: "identity",
            table: "devices",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_consents_subject_not_max_uuid",
            schema: "identity",
            table: "consents",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_bulk_exports_actor_not_max_uuid",
            schema: "identity",
            table: "bulk_exports",
            sql: "actor <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_break_glass_credentials_issued_by_not_max_uuid",
            schema: "identity",
            table: "break_glass_credentials",
            sql: "issued_by <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_authenticators_subject_not_max_uuid",
            schema: "identity",
            table: "authenticators",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_subject_not_max_uuid",
            schema: "identity",
            table: "accounts",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_account_preferences_subject_not_max_uuid",
            schema: "identity",
            table: "account_preferences",
            sql: "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            ALTER TABLE identity.audit_records DROP CONSTRAINT ck_audit_records_acting_subject_not_max_uuid;
            ALTER TABLE identity.audit_records DROP CONSTRAINT ck_audit_records_effective_subject_not_max_uuid;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_links_subject_not_max_uuid",
            schema: "identity",
            table: "signin_links");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_subject_not_max_uuid",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropCheckConstraint(
            name: "ck_sessions_subject_not_max_uuid",
            schema: "identity",
            table: "sessions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_send_outbox_subject_not_max_uuid",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_resources_subject_not_max_uuid",
            schema: "identity",
            table: "resources");

        migrationBuilder.DropCheckConstraint(
            name: "ck_registration_sessions_provisional_subject_not_max_uuid",
            schema: "identity",
            table: "registration_sessions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_links_approver_not_max_uuid",
            schema: "identity",
            table: "recovery_links");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_links_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_links");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_codes_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_codes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_code_sets_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_code_sets");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_approvals_approver_not_max_uuid",
            schema: "identity",
            table: "recovery_approvals");

        migrationBuilder.DropCheckConstraint(
            name: "ck_recovery_approvals_subject_not_max_uuid",
            schema: "identity",
            table: "recovery_approvals");

        migrationBuilder.DropCheckConstraint(
            name: "ck_read_volume_actor_not_max_uuid",
            schema: "identity",
            table: "read_volume");

        migrationBuilder.DropCheckConstraint(
            name: "ck_read_baselines_actor_not_max_uuid",
            schema: "identity",
            table: "read_baselines");

        migrationBuilder.DropCheckConstraint(
            name: "ck_profiles_subject_not_max_uuid",
            schema: "identity",
            table: "profiles");

        migrationBuilder.DropCheckConstraint(
            name: "ck_profile_photos_subject_not_max_uuid",
            schema: "identity",
            table: "profile_photos");

        migrationBuilder.DropCheckConstraint(
            name: "ck_privacy_requests_subject_not_max_uuid",
            schema: "identity",
            table: "privacy_requests");

        migrationBuilder.DropCheckConstraint(
            name: "ck_privacy_exports_subject_not_max_uuid",
            schema: "identity",
            table: "privacy_exports");

        migrationBuilder.DropCheckConstraint(
            name: "ck_passwords_subject_not_max_uuid",
            schema: "identity",
            table: "passwords");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_subject_not_max_uuid",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_tokens_subject_not_max_uuid",
            schema: "identity",
            table: "oidc_tokens");

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_authorizations_subject_not_max_uuid",
            schema: "identity",
            table: "oidc_authorizations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_objections_subject_not_max_uuid",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropCheckConstraint(
            name: "ck_memberships_subject_not_max_uuid",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropCheckConstraint(
            name: "ck_maintenance_log_actor_not_max_uuid",
            schema: "identity",
            table: "maintenance_log");

        migrationBuilder.DropCheckConstraint(
            name: "ck_mailboxes_holder_not_max_uuid",
            schema: "identity",
            table: "mailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "ck_loss_reports_subject_not_max_uuid",
            schema: "identity",
            table: "loss_reports");

        migrationBuilder.DropCheckConstraint(
            name: "ck_lifecycle_links_subject_not_max_uuid",
            schema: "identity",
            table: "lifecycle_links");

        migrationBuilder.DropCheckConstraint(
            name: "ck_key_ceremonies_subject_not_max_uuid",
            schema: "identity",
            table: "key_ceremonies");

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_invitee_not_max_uuid",
            schema: "identity",
            table: "invitations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_invitations_inviter_not_max_uuid",
            schema: "identity",
            table: "invitations");

        migrationBuilder.DropCheckConstraint(
            name: "ck_identifiers_subject_not_max_uuid",
            schema: "identity",
            table: "identifiers");

        migrationBuilder.DropCheckConstraint(
            name: "ck_identifier_verifications_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_verifications");

        migrationBuilder.DropCheckConstraint(
            name: "ck_identifier_removals_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_removals");

        migrationBuilder.DropCheckConstraint(
            name: "ck_identifier_backup_settings_subject_not_max_uuid",
            schema: "identity",
            table: "identifier_backup_settings");

        migrationBuilder.DropCheckConstraint(
            name: "ck_group_members_member_id_not_max_uuid",
            schema: "identity",
            table: "group_members");

        migrationBuilder.DropCheckConstraint(
            name: "ck_group_closure_member_id_not_max_uuid",
            schema: "identity",
            table: "group_closure");

        migrationBuilder.DropCheckConstraint(
            name: "ck_grants_granted_by_not_max_uuid",
            schema: "identity",
            table: "grants");

        migrationBuilder.DropCheckConstraint(
            name: "ck_grants_revoked_by_not_max_uuid",
            schema: "identity",
            table: "grants");

        migrationBuilder.DropCheckConstraint(
            name: "ck_grants_subject_id_not_max_uuid",
            schema: "identity",
            table: "grants");

        migrationBuilder.DropCheckConstraint(
            name: "ck_grant_versions_subject_not_max_uuid",
            schema: "identity",
            table: "grant_versions");

        migrationBuilder.DropCheckConstraint(
            name: "ck_erasures_subject_not_max_uuid",
            schema: "identity",
            table: "erasures");

        migrationBuilder.DropCheckConstraint(
            name: "ck_devices_subject_not_max_uuid",
            schema: "identity",
            table: "devices");

        migrationBuilder.DropCheckConstraint(
            name: "ck_consents_subject_not_max_uuid",
            schema: "identity",
            table: "consents");

        migrationBuilder.DropCheckConstraint(
            name: "ck_bulk_exports_actor_not_max_uuid",
            schema: "identity",
            table: "bulk_exports");

        migrationBuilder.DropCheckConstraint(
            name: "ck_break_glass_credentials_issued_by_not_max_uuid",
            schema: "identity",
            table: "break_glass_credentials");

        migrationBuilder.DropCheckConstraint(
            name: "ck_authenticators_subject_not_max_uuid",
            schema: "identity",
            table: "authenticators");

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_subject_not_max_uuid",
            schema: "identity",
            table: "accounts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_account_preferences_subject_not_max_uuid",
            schema: "identity",
            table: "account_preferences");
    }
}
