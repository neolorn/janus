using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Npgsql;
using NpgsqlTypes;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The volumes AUTHZ-TEST-002 names, written once so a query plan is read against
/// something the deployment will actually hold: a million records, a million grants of
/// which a tenth are revoked, a hundred thousand principals and ten thousand groups.
/// </summary>
/// <param name="fixture">The deployment the rows are written to.</param>
/// <remarks>
/// OPS-DATA-003: the rows go in through binary bulk copy, which is one of the database
/// features named there as having no higher-level abstraction. Nothing here is the
/// library's own writer: the point is a database in a stated shape. One connection
/// carries one bulk copy at a time, so each table is written in a pass of its own.
/// </remarks>
internal sealed class ProductionVolume(HostFixture fixture)
{
    /// <summary>
    /// How many records the host holds.
    /// </summary>
    public const int Resources = 1_000_000;

    /// <summary>
    /// How many grants stand, revoked ones counted.
    /// </summary>
    public const int Grants = 1_000_000;

    /// <summary>
    /// How many of those are revoked.
    /// </summary>
    public const int Revoked = Grants / 10;

    /// <summary>
    /// How many accounts hold them.
    /// </summary>
    public const int Principals = 100_000;

    /// <summary>
    /// How many groups they belong to.
    /// </summary>
    public const int Groups = 10_000;

    /// <summary>
    /// How many of the records are containers.
    /// </summary>
    public const int Containers = 10_000;

    /// <summary>
    /// How many of the records sit inside one.
    /// </summary>
    public const int Contained = Resources - Containers;

    /// <summary>
    /// How many records each container holds.
    /// </summary>
    public const int PerContainer = Contained / Containers;

    /// <summary>
    /// How many facts the host's own relation holds, one derivation following from it.
    /// </summary>
    public const int Reviewers = Principals;

    private const string Workspace = "workspace";
    private const string Document = "document";

    private static readonly DateTimeOffset At = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid[] _accounts = new Guid[Principals];
    private readonly Guid[] _groups = new Guid[Groups];

    /// <summary>
    /// The organization everything belongs to.
    /// </summary>
    public OrganizationId Organization { get; } = new(Guid.NewGuid());

    /// <summary>
    /// The account the plan is read for. It holds nothing itself and belongs to one
    /// group holding a grant on the first container, which is the longer of the two
    /// ways a grant reaches a record.
    /// </summary>
    public SubjectId Reader => new(_accounts[0]);

    /// <summary>
    /// Writes every row.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing them.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await FoundationAsync(connection, cancellationToken);
        await AccountsAsync(connection, cancellationToken);
        await GroupsAsync(connection, cancellationToken);
        await RecordsAsync(connection, cancellationToken);
        await GrantsAsync(connection, cancellationToken);
        await ReviewersAsync(connection, cancellationToken);

        // Without statistics the planner has nothing to choose an index by, and a plan
        // read before them says nothing about the deployment.
        await connection.ExecuteAsync(new CommandDefinition(
            "ANALYZE identity.grants, identity.ancestry, identity.resources, identity.group_closure, "
            + "identity.group_members, identity.role_permissions, identity.accounts, identity.groups, "
            + "host.documents, host.reviewers;",
            commandTimeout: 600,
            cancellationToken: cancellationToken));
    }

    private static string Identifier(string kind, int at) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}-{at:D7}");

    // A million rows go in while the rest of the suite holds its own containers, so
    // the wait is the deployment's and not the thirty seconds a command has by default.
    private static async Task<NpgsqlBinaryImporter> ImporterAsync(
        NpgsqlConnection connection,
        string copy,
        CancellationToken cancellationToken)
    {
        NpgsqlBinaryImporter importer =
            await connection.BeginBinaryImportAsync(copy, cancellationToken);

        importer.Timeout = TimeSpan.FromSeconds(600);

        return importer;
    }

    private async Task FoundationAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO identity.organizations (id, name, created_at)
            VALUES (@organization, @name, @at);
            INSERT INTO identity.roles (name) VALUES ('reader'), ('reviewer');
            INSERT INTO identity.role_permissions (role, permission)
            VALUES ('reader', @read), ('reader', @edit), ('reviewer', @read);
            """,
            new
            {
                organization = Organization.Value,
                name = "The organization at production volume",
                at = At,
                read = HostPermissions.Read.ToString(),
                edit = HostPermissions.Edit.ToString(),
            },
            cancellationToken: cancellationToken));
    }

    private async Task AccountsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using NpgsqlBinaryImporter accounts = await ImporterAsync(
            connection,
            "COPY identity.accounts (subject, created_at, state) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        for (int at = 0; at < Principals; at++)
        {
            _accounts[at] = Guid.CreateVersion7();

            await accounts.StartRowAsync(cancellationToken);
            await accounts.WriteAsync(_accounts[at], NpgsqlDbType.Uuid, cancellationToken);
            await accounts.WriteAsync(At, NpgsqlDbType.TimestampTz, cancellationToken);
            await accounts.WriteAsync("active", NpgsqlDbType.Text, cancellationToken);
        }

        await accounts.CompleteAsync(cancellationToken);
    }

    private async Task GroupsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using (NpgsqlBinaryImporter groups = await ImporterAsync(
            connection,
            "COPY identity.groups (id, organization, name) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            for (int at = 0; at < Groups; at++)
            {
                _groups[at] = Guid.CreateVersion7();

                await groups.StartRowAsync(cancellationToken);
                await groups.WriteAsync(_groups[at], NpgsqlDbType.Uuid, cancellationToken);
                await groups.WriteAsync(Organization.Value, NpgsqlDbType.Uuid, cancellationToken);
                await groups.WriteAsync(Identifier("group", at), NpgsqlDbType.Text, cancellationToken);
            }

            await groups.CompleteAsync(cancellationToken);
        }

        // Every account is in one group, each group holding ten of them, which is the
        // shape the closure is read by (AUTHZ-GROUP-002).
        await using (NpgsqlBinaryImporter members = await ImporterAsync(
            connection,
            "COPY identity.group_members (group_id, member_type, member_id) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            for (int at = 0; at < Principals; at++)
            {
                await members.StartRowAsync(cancellationToken);
                await members.WriteAsync(_groups[at % Groups], NpgsqlDbType.Uuid, cancellationToken);
                await members.WriteAsync("user", NpgsqlDbType.Text, cancellationToken);
                await members.WriteAsync(_accounts[at], NpgsqlDbType.Uuid, cancellationToken);
            }

            await members.CompleteAsync(cancellationToken);
        }

        await using NpgsqlBinaryImporter closure = await ImporterAsync(
            connection,
            "COPY identity.group_closure (group_id, member_type, member_id, depth) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        for (int at = 0; at < Principals; at++)
        {
            await closure.StartRowAsync(cancellationToken);
            await closure.WriteAsync(_groups[at % Groups], NpgsqlDbType.Uuid, cancellationToken);
            await closure.WriteAsync("user", NpgsqlDbType.Text, cancellationToken);
            await closure.WriteAsync(_accounts[at], NpgsqlDbType.Uuid, cancellationToken);
            await closure.WriteAsync(1, NpgsqlDbType.Integer, cancellationToken);
        }

        await closure.CompleteAsync(cancellationToken);
    }

    private async Task RecordsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using (NpgsqlBinaryImporter resources = await ImporterAsync(
            connection,
            "COPY identity.resources (resource_type, resource_id, organization, contained_in_type, "
            + "contained_in_id) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            for (int at = 0; at < Containers; at++)
            {
                await resources.StartRowAsync(cancellationToken);
                await resources.WriteAsync(Workspace, NpgsqlDbType.Text, cancellationToken);
                await resources.WriteAsync(
                    Identifier(Workspace, at),
                    NpgsqlDbType.Text,
                    cancellationToken);
                await resources.WriteAsync(Organization.Value, NpgsqlDbType.Uuid, cancellationToken);
                await resources.WriteNullAsync(cancellationToken);
                await resources.WriteNullAsync(cancellationToken);
            }

            for (int at = 0; at < Contained; at++)
            {
                await resources.StartRowAsync(cancellationToken);
                await resources.WriteAsync(Document, NpgsqlDbType.Text, cancellationToken);
                await resources.WriteAsync(
                    Identifier(Document, at),
                    NpgsqlDbType.Text,
                    cancellationToken);
                await resources.WriteAsync(Organization.Value, NpgsqlDbType.Uuid, cancellationToken);
                await resources.WriteAsync(Workspace, NpgsqlDbType.Text, cancellationToken);
                await resources.WriteAsync(
                    Identifier(Workspace, at / PerContainer),
                    NpgsqlDbType.Text,
                    cancellationToken);
            }

            await resources.CompleteAsync(cancellationToken);
        }

        await using (NpgsqlBinaryImporter ancestry = await ImporterAsync(
            connection,
            "COPY identity.ancestry (resource_type, resource_id, ancestor_type, ancestor_id, depth, "
            + "organization) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            for (int at = 0; at < Containers; at++)
            {
                string container = Identifier(Workspace, at);

                await AncestorAsync(
                    ancestry, Workspace, container, Workspace, container, 0, cancellationToken);
            }

            for (int at = 0; at < Contained; at++)
            {
                string record = Identifier(Document, at);
                string container = Identifier(Workspace, at / PerContainer);

                await AncestorAsync(
                    ancestry, Document, record, Document, record, 0, cancellationToken);
                await AncestorAsync(
                    ancestry, Document, record, Workspace, container, 1, cancellationToken);
            }

            await ancestry.CompleteAsync(cancellationToken);
        }

        await using NpgsqlBinaryImporter documents = await ImporterAsync(
            connection,
            "COPY host.documents (id, title) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        for (int at = 0; at < Contained; at++)
        {
            await documents.StartRowAsync(cancellationToken);
            await documents.WriteAsync(Identifier(Document, at), NpgsqlDbType.Text, cancellationToken);
            await documents.WriteAsync("A record of the host's.", NpgsqlDbType.Text, cancellationToken);
        }

        await documents.CompleteAsync(cancellationToken);
    }

    // The fact a derivation follows from, at the volume the host's own relation would
    // hold it at: every container reviewed by ten of the principals, the first of them
    // the account the plan is read for (AUTHZ-DERIVE-004).
    private async Task ReviewersAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlBinaryImporter reviewers = await ImporterAsync(
            connection,
            "COPY host.reviewers (workspace_id, reviewer) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        for (int at = 0; at < Reviewers; at++)
        {
            await reviewers.StartRowAsync(cancellationToken);
            await reviewers.WriteAsync(
                Identifier(Workspace, at % Containers),
                NpgsqlDbType.Text,
                cancellationToken);
            await reviewers.WriteAsync(_accounts[at], NpgsqlDbType.Uuid, cancellationToken);
        }

        await reviewers.CompleteAsync(cancellationToken);
    }

    private async Task GrantsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlBinaryImporter grants = await ImporterAsync(
            connection,
            "COPY identity.grants (id, subject_type, subject_id, role, organization, resource_type, "
            + "resource_id, deny, kind, expires_at, granted_by, granted_at, reason, revoked_by, "
            + "revoked_at, revocation_reason) FROM STDIN (FORMAT BINARY)",
            cancellationToken);

        for (int at = 0; at < Grants; at++)
        {
            bool toAGroup = at % 10 == 0;
            bool revoked = at % 10 == 9;

            await grants.StartRowAsync(cancellationToken);
            await grants.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid, cancellationToken);
            await grants.WriteAsync(toAGroup ? "group" : "user", NpgsqlDbType.Text, cancellationToken);
            await grants.WriteAsync(
                toAGroup ? _groups[at % Groups] : _accounts[at % Principals],
                NpgsqlDbType.Uuid,
                cancellationToken);
            await grants.WriteAsync("reader", NpgsqlDbType.Text, cancellationToken);
            await grants.WriteAsync(Organization.Value, NpgsqlDbType.Uuid, cancellationToken);
            await grants.WriteAsync(Workspace, NpgsqlDbType.Text, cancellationToken);
            await grants.WriteAsync(
                Identifier(Workspace, at % Containers),
                NpgsqlDbType.Text,
                cancellationToken);
            await grants.WriteAsync(false, NpgsqlDbType.Boolean, cancellationToken);
            await grants.WriteAsync("stored", NpgsqlDbType.Text, cancellationToken);
            await grants.WriteNullAsync(cancellationToken);
            await grants.WriteAsync(_accounts[0], NpgsqlDbType.Uuid, cancellationToken);
            await grants.WriteAsync(At, NpgsqlDbType.TimestampTz, cancellationToken);
            await grants.WriteAsync(
                "The reason the grant was written.",
                NpgsqlDbType.Text,
                cancellationToken);

            if (revoked)
            {
                await grants.WriteAsync(_accounts[0], NpgsqlDbType.Uuid, cancellationToken);
                await grants.WriteAsync(At, NpgsqlDbType.TimestampTz, cancellationToken);
                await grants.WriteAsync(
                    "The reason the grant was taken away.",
                    NpgsqlDbType.Text,
                    cancellationToken);
            }
            else
            {
                await grants.WriteNullAsync(cancellationToken);
                await grants.WriteNullAsync(cancellationToken);
                await grants.WriteNullAsync(cancellationToken);
            }
        }

        await grants.CompleteAsync(cancellationToken);
    }

    private async Task AncestorAsync(
        NpgsqlBinaryImporter ancestry,
        string type,
        string record,
        string ancestorType,
        string ancestor,
        int depth,
        CancellationToken cancellationToken)
    {
        await ancestry.StartRowAsync(cancellationToken);
        await ancestry.WriteAsync(type, NpgsqlDbType.Text, cancellationToken);
        await ancestry.WriteAsync(record, NpgsqlDbType.Text, cancellationToken);
        await ancestry.WriteAsync(ancestorType, NpgsqlDbType.Text, cancellationToken);
        await ancestry.WriteAsync(ancestor, NpgsqlDbType.Text, cancellationToken);
        await ancestry.WriteAsync(depth, NpgsqlDbType.Integer, cancellationToken);
        await ancestry.WriteAsync(Organization.Value, NpgsqlDbType.Uuid, cancellationToken);
    }
}
