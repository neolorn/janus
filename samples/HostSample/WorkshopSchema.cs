namespace HostSample;

/// <summary>
/// The workshop's own tables, which the workshop creates in the deployment's database
/// beside the library's, with the index each fact a role is derived from is read
/// through (AUTHZ-DERIVE-004).
/// </summary>
public static class WorkshopSchema
{
    /// <summary>
    /// The schema the workshop's tables are in.
    /// </summary>
    public const string Name = "workshop";

    /// <summary>
    /// The statements that create the workshop's tables, run once before the application
    /// first starts.
    /// </summary>
    public const string Creation =
        """
        CREATE SCHEMA workshop;
        CREATE TABLE workshop.workshops (
            id text PRIMARY KEY,
            organization uuid NOT NULL,
            name text NOT NULL,
            capacity integer NOT NULL);
        CREATE TABLE workshop.repairs (
            id text PRIMARY KEY,
            workshop_id text NOT NULL REFERENCES workshop.workshops (id),
            customer uuid NOT NULL,
            kite text NOT NULL,
            contact_name bytea NOT NULL,
            follow_up boolean NOT NULL,
            released_at timestamptz);
        CREATE INDEX ix_repairs_workshop_id ON workshop.repairs (workshop_id);
        CREATE INDEX ix_repairs_customer ON workshop.repairs (customer);
        CREATE TABLE workshop.owners (
            repair_id text NOT NULL REFERENCES workshop.repairs (id),
            owner uuid NOT NULL,
            PRIMARY KEY (repair_id, owner));
        CREATE INDEX ix_owners_owner ON workshop.owners (owner);
        CREATE TABLE workshop.collectors (
            repair_id text NOT NULL REFERENCES workshop.repairs (id),
            collector uuid NOT NULL,
            PRIMARY KEY (repair_id, collector));
        CREATE INDEX ix_collectors_collector ON workshop.collectors (collector);
        CREATE TABLE workshop.fittings (
            id text PRIMARY KEY,
            repair_id text NOT NULL REFERENCES workshop.repairs (id),
            customer uuid NOT NULL,
            need bytea NOT NULL);
        CREATE INDEX ix_fittings_repair_id ON workshop.fittings (repair_id);
        CREATE INDEX ix_fittings_customer ON workshop.fittings (customer);
        """;
}
