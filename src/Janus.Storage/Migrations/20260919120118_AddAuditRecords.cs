using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAuditRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-RET-002: the trail is partitioned by retention category and then by
        // calendar month on the instant the event occurred, so a month of one category
        // is dropped whole when its end passes that category's retention. No model
        // builder expresses declarative partitioning, so the table is written here and
        // the mapping is excluded from migrations; the two are read together.
        migrationBuilder.Sql(
            """
            CREATE TABLE janus.audit_records (
                id uuid NOT NULL,
                category text NOT NULL,
                occurred_at timestamp with time zone NOT NULL,
                action text NOT NULL,
                acting_subject uuid NOT NULL,
                effective_subject uuid NOT NULL,
                organization uuid,
                details jsonb NOT NULL,
                enc_details bytea,
                CONSTRAINT pk_audit_records PRIMARY KEY (category, occurred_at, id)
            ) PARTITION BY LIST (category);
            """);

        // CONV-ENUM-001: the admitted categories are the two partitions and nothing
        // else, so a row of any other category has nowhere to go.
        migrationBuilder.Sql(
            """
            CREATE TABLE janus.audit_records_security
                PARTITION OF janus.audit_records FOR VALUES IN ('security')
                PARTITION BY RANGE (occurred_at);
            """);

        migrationBuilder.Sql(
            """
            CREATE TABLE janus.audit_records_routine
                PARTITION OF janus.audit_records FOR VALUES IN ('routine')
                PARTITION BY RANGE (occurred_at);
            """);

        // PRIV-BREACH-002: every record of one subject, without a full scan.
        migrationBuilder.Sql(
            """
            CREATE INDEX ix_audit_records_effective_subject
                ON janus.audit_records (effective_subject, occurred_at);
            """);

        // PRIV-RET-002, OPS-MIG-003a: the application holds no schema rights, so the
        // months are created by a function that does exactly this and runs with the
        // rights of the migration role that owns it.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION janus.audit_ensure_partitions() RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, pg_temp
            AS $function$
            DECLARE
                parent text;
                ahead integer;
                starts timestamp with time zone;
                ends timestamp with time zone;
                leaf text;
                created integer := 0;
            BEGIN
                FOREACH parent IN ARRAY ARRAY['audit_records_security', 'audit_records_routine'] LOOP
                    FOR ahead IN 0..2 LOOP
                        starts := (date_trunc('month', now() AT TIME ZONE 'UTC')
                            + (ahead || ' month')::interval) AT TIME ZONE 'UTC';
                        ends := (starts AT TIME ZONE 'UTC' + interval '1 month') AT TIME ZONE 'UTC';
                        leaf := parent || '_' || to_char(starts AT TIME ZONE 'UTC', 'YYYY_MM');

                        IF to_regclass('janus.' || quote_ident(leaf)) IS NULL THEN
                            EXECUTE format(
                                'CREATE TABLE janus.%I PARTITION OF janus.%I '
                                    || 'FOR VALUES FROM (%L) TO (%L)',
                                leaf, parent, starts, ends);
                            created := created + 1;
                        END IF;
                    END LOOP;
                END LOOP;

                RETURN created;
            END;
            $function$;
            """);

        // The table takes no row until the month it falls in exists, so the months the
        // sweep would create are created here as well.
        migrationBuilder.Sql("SELECT janus.audit_ensure_partitions();");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("DROP FUNCTION janus.audit_ensure_partitions();");
        migrationBuilder.Sql("DROP TABLE janus.audit_records;");
    }
}
