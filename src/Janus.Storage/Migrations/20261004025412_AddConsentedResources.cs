using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddConsentedResources : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTHZ-GATE-002, PRIV-SENS-002, LIB-API-001: each registered record whose data
        // subject holds a consent that is neither withdrawn nor superseded, one row per
        // purpose, with the document and the kind the consent was recorded with. It is
        // a view so that a withdrawal or a supersession takes the record out of the
        // next list and nothing is stored twice. A subject holds one live consent a
        // purpose, so a record and a purpose name one row.
        migrationBuilder.Sql(
            """
            CREATE VIEW identity.consented_resources AS
            SELECT r.resource_type AS resource_type,
                   r.resource_id   AS resource_id,
                   c.purpose       AS purpose,
                   c.document      AS document,
                   c.kind          AS kind
            FROM identity.resources AS r
            JOIN identity.consents AS c ON c.subject = r.subject
            WHERE c.withdrawn_at IS NULL
              AND c.superseded_at IS NULL;
            """);

        // OPS-MIG-003 AC1: the application reads the view and alters nothing.
        migrationBuilder.Sql("GRANT SELECT ON identity.consented_resources TO identity_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("DROP VIEW identity.consented_resources;");
    }
}
