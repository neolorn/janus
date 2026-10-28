using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddPersonRecords : Migration
{
    private static readonly string[] KindAndFingerprint = ["kind", "fingerprint"];

    private static readonly (string Table, string Constraint)[] SubjectKeyChildren =
    [
        ("person_records", "fk_person_records_subject"),
        ("consents", "fk_consents_subject"),
        ("objections", "fk_objections_subject"),
        ("privacy_exports", "fk_privacy_exports_subject"),
        ("privacy_requests", "fk_privacy_requests_subject"),
        ("erasures", "fk_erasures_subject"),
        ("outbox", "fk_outbox_subject"),
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropForeignKey(
            name: "fk_consents_subject",
            schema: "identity",
            table: "consents");

        migrationBuilder.DropForeignKey(
            name: "fk_erasures_subject",
            schema: "identity",
            table: "erasures");

        migrationBuilder.DropForeignKey(
            name: "fk_objections_subject",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropForeignKey(
            name: "fk_outbox_subject",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropForeignKey(
            name: "fk_privacy_exports_subject",
            schema: "identity",
            table: "privacy_exports");

        migrationBuilder.DropForeignKey(
            name: "fk_privacy_requests_subject",
            schema: "identity",
            table: "privacy_requests");

        migrationBuilder.CreateTable(
            name: "person_records",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                origin = table.Column<string>(type: "text", nullable: false),
                state = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                language = table.Column<string>(type: "text", nullable: false),
                enc_name = table.Column<byte[]>(type: "bytea", nullable: true),
                restriction_held = table.Column<bool>(type: "boolean", nullable: false),
                deleting_by = table.Column<string>(type: "text", nullable: true),
                deleting_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                deletion_held = table.Column<string>(type: "text", nullable: true),
                deletion_held_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attached_to = table.Column<Guid>(type: "uuid", nullable: true),
                attached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                adult_affirmed = table.Column<bool>(type: "boolean", nullable: true),
                age_group = table.Column<string>(type: "text", nullable: true),
                answered_age_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                enc_date_of_birth = table.Column<byte[]>(type: "bytea", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_person_records", x => x.subject);
                table.CheckConstraint("ck_person_records_age_answer", "adult_affirmed IS NULL OR age_group IS NULL");
                table.CheckConstraint("ck_person_records_age_group", "age_group IS NULL OR age_group IN ('adult', 'minor')");
                table.CheckConstraint("ck_person_records_answered_age_at", "(answered_age_at IS NULL) = (adult_affirmed IS NULL AND age_group IS NULL)");
                table.CheckConstraint("ck_person_records_attached", "(attached_to IS NULL) = (attached_at IS NULL)");
                table.CheckConstraint("ck_person_records_attached_to_not_max_uuid", "attached_to <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.CheckConstraint("ck_person_records_date_of_birth", "enc_date_of_birth IS NULL OR answered_age_at IS NOT NULL");
                table.CheckConstraint("ck_person_records_deleting", "(deleting_by IS NULL) = (deleting_since IS NULL)");
                table.CheckConstraint("ck_person_records_deleting_by", "deleting_by IS NULL OR deleting_by IN ('oob-request', 'self', 'takedown')");
                table.CheckConstraint("ck_person_records_deletion_held", "deletion_held IS NULL OR deletion_held IN ('oob-request', 'self')");
                table.CheckConstraint("ck_person_records_deletion_held_since", "(deletion_held IS NULL) = (deletion_held_since IS NULL)");
                table.CheckConstraint("ck_person_records_deletion_held_state", "deletion_held IS NULL OR (state = 'deleting' AND deleting_by = 'takedown')");
                table.CheckConstraint("ck_person_records_origin", "origin IN ('collection', 'staff')");
                table.CheckConstraint("ck_person_records_restriction_held", "NOT restriction_held OR state = 'deleting'");
                table.CheckConstraint("ck_person_records_state", "state IN ('active', 'deleted', 'deleting', 'restricted')");
                table.CheckConstraint("ck_person_records_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.ForeignKey(
                    name: "fk_person_records_attached_to",
                    column: x => x.attached_to,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_person_records_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "person_record_contacts",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                fingerprint_version = table.Column<int>(type: "integer", nullable: false),
                canonicalisation_version = table.Column<string>(type: "text", nullable: false),
                enc_value = table.Column<byte[]>(type: "bytea", nullable: false),
                verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_person_record_contacts", x => new { x.subject, x.kind });
                table.CheckConstraint("ck_person_record_contacts_fingerprint", "octet_length(fingerprint) = 32");
                table.CheckConstraint("ck_person_record_contacts_kind", "kind IN ('email', 'phone')");
                table.CheckConstraint("ck_person_record_contacts_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.ForeignKey(
                    name: "fk_person_record_contacts_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "person_records",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "person_record_documents",
            schema: "identity",
            columns: table => new
            {
                documents_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                notice_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                terms_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_person_record_documents", x => x.documents_id);
                table.CheckConstraint("ck_person_record_documents_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.ForeignKey(
                    name: "fk_person_record_documents_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "person_records",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "person_record_references",
            schema: "identity",
            columns: table => new
            {
                reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                fingerprint_version = table.Column<int>(type: "integer", nullable: false),
                added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_person_record_references", x => x.reference_id);
                table.CheckConstraint("ck_person_record_references_fingerprint", "octet_length(fingerprint) = 32");
                table.CheckConstraint("ck_person_record_references_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
                table.ForeignKey(
                    name: "fk_person_record_references_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "person_records",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_person_record_contacts_fingerprint",
            schema: "identity",
            table: "person_record_contacts",
            columns: KindAndFingerprint,
            filter: "fingerprint <> decode(repeat('00', 32), 'hex')");

        migrationBuilder.CreateIndex(
            name: "ix_person_record_documents_subject",
            schema: "identity",
            table: "person_record_documents",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_person_record_references_fingerprint",
            schema: "identity",
            table: "person_record_references",
            column: "fingerprint",
            filter: "fingerprint <> decode(repeat('00', 32), 'hex')");

        migrationBuilder.CreateIndex(
            name: "ix_person_record_references_subject",
            schema: "identity",
            table: "person_record_references",
            column: "subject");

        migrationBuilder.CreateIndex(
            name: "ix_person_records_attached_to",
            schema: "identity",
            table: "person_records",
            column: "attached_to",
            filter: "attached_to IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_person_records_deleting_since",
            schema: "identity",
            table: "person_records",
            column: "deleting_since",
            filter: "deleting_since IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_person_records_organization",
            schema: "identity",
            table: "person_records",
            column: "organization");

        // REC-REC-001 (D-221): a record's data key is a row of the subject-key table as an
        // account's is, and a subject's consents, objections, exports, privacy requests,
        // erasure and outbox rows name an account or a record alike, so the key row both
        // hold is their parent. The model cannot name it, the key's identifier being a
        // type of its own, so the constraints are written here.
        foreach ((string table, string constraint) in SubjectKeyChildren)
        {
            migrationBuilder.AddForeignKey(
                name: constraint,
                schema: "identity",
                table: table,
                column: "subject",
                principalSchema: "identity",
                principalTable: "subject_keys",
                principalColumn: "subject",
                onDelete: ReferentialAction.Restrict);
        }

        // OPS-MIG-003: the application reads and writes the records it keeps.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                identity.person_records,
                identity.person_record_contacts,
                identity.person_record_references,
                identity.person_record_documents
            TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        foreach ((string table, string constraint) in SubjectKeyChildren)
        {
            migrationBuilder.DropForeignKey(
                name: constraint,
                schema: "identity",
                table: table);
        }

        migrationBuilder.DropTable(
            name: "person_record_contacts",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "person_record_documents",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "person_record_references",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "person_records",
            schema: "identity");

        migrationBuilder.AddForeignKey(
            name: "fk_consents_subject",
            schema: "identity",
            table: "consents",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_erasures_subject",
            schema: "identity",
            table: "erasures",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_objections_subject",
            schema: "identity",
            table: "objections",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_outbox_subject",
            schema: "identity",
            table: "outbox",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_privacy_exports_subject",
            schema: "identity",
            table: "privacy_exports",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_privacy_requests_subject",
            schema: "identity",
            table: "privacy_requests",
            column: "subject",
            principalSchema: "identity",
            principalTable: "accounts",
            principalColumn: "subject",
            onDelete: ReferentialAction.Restrict);
    }
}
