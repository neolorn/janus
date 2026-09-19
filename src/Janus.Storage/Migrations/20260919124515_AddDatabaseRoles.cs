using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddDatabaseRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-MIG-003, OPS-MIG-003a: the two runtime roles exist before anything is
        // granted to them. Neither signs in on its own; the deployment attaches a
        // credential to each from the secrets manager (INF-HOST-003).
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'janus_app') THEN
                    CREATE ROLE janus_app NOLOGIN;
                END IF;

                IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'janus_maintenance') THEN
                    CREATE ROLE janus_maintenance NOLOGIN;
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.Sql("GRANT USAGE ON SCHEMA janus TO janus_app, janus_maintenance;");

        // OPS-MIG-003 AC1: the application reads and writes rows and alters nothing.
        // The tables are named one by one so that a table added later is reached only
        // by a migration that says so.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                janus.accounts,
                janus.account_preferences,
                janus.erasures,
                janus.identifiers,
                janus.identifier_backup_settings,
                janus.memberships,
                janus.organizations,
                janus.profiles,
                janus.profile_photos,
                janus.settings,
                janus.subject_keys
            TO janus_app;
            """);

        // PRIV-RET-002 AC1: the trail is append-only from the application, which the
        // database refuses rather than the code declining to write.
        migrationBuilder.Sql("GRANT SELECT, INSERT ON janus.audit_records TO janus_app;");

        // PRIV-RET-002: a partition whose end has passed its category's retention is
        // dropped by a function that does exactly this and runs with the rights of the
        // role that owns it. The two retentions arrive as arguments because a key at
        // its default has no settings row the database could read, and each is held to
        // the PRIV-RET-001 floor, so the caller cannot shorten retention by argument.
        migrationBuilder.Sql(
            """
            CREATE FUNCTION janus.audit_drop_expired_partitions(
                security_retention interval, routine_retention interval) RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, pg_temp
            AS $function$
            DECLARE
                leaf record;
                retention interval;
                ends timestamp with time zone;
                dropped integer := 0;
            BEGIN
                IF security_retention < interval '5 years' THEN
                    RAISE EXCEPTION 'The security retention is below its floor.';
                END IF;

                IF routine_retention < interval '30 days' THEN
                    RAISE EXCEPTION 'The routine retention is below its floor.';
                END IF;

                FOR leaf IN
                    SELECT child.relname AS name, parent.relname AS category
                    FROM pg_class child
                    JOIN pg_inherits ON pg_inherits.inhrelid = child.oid
                    JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
                    JOIN pg_namespace space ON space.oid = child.relnamespace
                    WHERE space.nspname = 'janus'
                        AND parent.relname IN ('audit_records_security', 'audit_records_routine')
                LOOP
                    retention := CASE leaf.category
                        WHEN 'audit_records_security' THEN security_retention
                        ELSE routine_retention
                    END;

                    ends := (to_date(right(leaf.name, 7), 'YYYY_MM')
                        + interval '1 month') AT TIME ZONE 'UTC';

                    IF ends < now() - retention THEN
                        EXECUTE format('DROP TABLE janus.%I', leaf.name);
                        dropped := dropped + 1;
                    END IF;
                END LOOP;

                RETURN dropped;
            END;
            $function$;
            """);

        // PRIV-RET-002 AC5, OPS-MIG-003a AC3: a function is executable by everyone
        // until that is taken away, so each is taken from everyone and given back to
        // the one role that runs it.
        migrationBuilder.Sql(
            """
            REVOKE ALL ON FUNCTION janus.audit_ensure_partitions() FROM PUBLIC;
            """);

        migrationBuilder.Sql(
            """
            REVOKE ALL ON FUNCTION
                janus.audit_drop_expired_partitions(interval, interval) FROM PUBLIC;
            """);

        migrationBuilder.Sql(
            """
            GRANT EXECUTE ON FUNCTION janus.audit_ensure_partitions() TO janus_maintenance;
            """);

        migrationBuilder.Sql(
            """
            GRANT EXECUTE ON FUNCTION
                janus.audit_drop_expired_partitions(interval, interval) TO janus_maintenance;
            """);

        // OPS-MIG-003a AC4: the re-wrap reads and writes the wrapped keys in the
        // command's own process, so the maintenance role reaches those rows and no
        // other table.
        migrationBuilder.Sql("GRANT SELECT, UPDATE ON janus.subject_keys TO janus_maintenance;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The roles are of the cluster rather than of this database, so what was
        // granted is taken back and the roles themselves are left where they are.
        migrationBuilder.Sql(
            """
            DROP FUNCTION janus.audit_drop_expired_partitions(interval, interval);
            """);

        migrationBuilder.Sql("REVOKE ALL ON ALL TABLES IN SCHEMA janus FROM janus_app;");

        migrationBuilder.Sql(
            """
            REVOKE ALL ON ALL TABLES IN SCHEMA janus FROM janus_maintenance;
            """);

        migrationBuilder.Sql(
            """
            REVOKE ALL ON FUNCTION janus.audit_ensure_partitions() FROM janus_maintenance;
            """);

        migrationBuilder.Sql(
            """
            REVOKE USAGE ON SCHEMA janus FROM janus_app, janus_maintenance;
            """);
    }
}
