using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class GrantApplicationTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-MIG-003: the application reads and writes the rows of every table the
        // migrations after the authorization one added, which none of them granted, and
        // reads the migration history its startup check compares against (OPS-MIG-002).
        // The tables are named one by one, as the earlier grants name theirs, so that a
        // table added later is reached only by a migration that says so.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                identity.alerts,
                identity.authenticators,
                identity.break_glass_attempts,
                identity.break_glass_credentials,
                identity.callback_events,
                identity.callback_references,
                identity.callbacks,
                identity.compliance_records,
                identity.consents,
                identity.devices,
                identity.identifier_removals,
                identity.identifier_verifications,
                identity.invitations,
                identity.key_ceremonies,
                identity.legal_document_translations,
                identity.legal_document_versions,
                identity.lifecycle_links,
                identity.loss_reports,
                identity.mailboxes,
                identity.nonexistence_notices,
                identity.objections,
                identity.oidc_authorizations,
                identity.oidc_clients,
                identity.oidc_scopes,
                identity.oidc_tokens,
                identity.organization_domains,
                identity.outbox,
                identity.outbox_confirmations,
                identity.passwords,
                identity.policy_raises,
                identity.preauthentication_sessions,
                identity.privacy_exports,
                identity.privacy_requests,
                identity.raised_alerts,
                identity.recovery_approvals,
                identity.recovery_code_sets,
                identity.recovery_codes,
                identity.recovery_links,
                identity.registration_links,
                identity.registration_sessions,
                identity.registration_sources,
                identity.send_counters,
                identity.send_grants,
                identity.send_outbox,
                identity.sends,
                identity.sessions,
                identity.signin_challenges,
                identity.signin_links,
                identity.signing_keys,
                identity.sms_balance_readings,
                identity.throttle_counters,
                identity.username_holds,
                identity.verification_codes
            TO identity_app;
            """);

        migrationBuilder.Sql("GRANT SELECT ON identity.__migrations_history TO identity_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("REVOKE SELECT ON identity.__migrations_history FROM identity_app;");

        migrationBuilder.Sql(
            """
            REVOKE SELECT, INSERT, UPDATE, DELETE ON
                identity.alerts,
                identity.authenticators,
                identity.break_glass_attempts,
                identity.break_glass_credentials,
                identity.callback_events,
                identity.callback_references,
                identity.callbacks,
                identity.compliance_records,
                identity.consents,
                identity.devices,
                identity.identifier_removals,
                identity.identifier_verifications,
                identity.invitations,
                identity.key_ceremonies,
                identity.legal_document_translations,
                identity.legal_document_versions,
                identity.lifecycle_links,
                identity.loss_reports,
                identity.mailboxes,
                identity.nonexistence_notices,
                identity.objections,
                identity.oidc_authorizations,
                identity.oidc_clients,
                identity.oidc_scopes,
                identity.oidc_tokens,
                identity.organization_domains,
                identity.outbox,
                identity.outbox_confirmations,
                identity.passwords,
                identity.policy_raises,
                identity.preauthentication_sessions,
                identity.privacy_exports,
                identity.privacy_requests,
                identity.raised_alerts,
                identity.recovery_approvals,
                identity.recovery_code_sets,
                identity.recovery_codes,
                identity.recovery_links,
                identity.registration_links,
                identity.registration_sessions,
                identity.registration_sources,
                identity.send_counters,
                identity.send_grants,
                identity.send_outbox,
                identity.sends,
                identity.sessions,
                identity.signin_challenges,
                identity.signin_links,
                identity.signing_keys,
                identity.sms_balance_readings,
                identity.throttle_counters,
                identity.username_holds,
                identity.verification_codes
            FROM identity_app;
            """);
    }
}
