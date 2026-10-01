using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The deployment's data key as the subject-key table holds it: under the max UUID of
/// RFC 9562, never the nil subject, and the migration that moved it there
/// (PRIV-RIGHT-005a, D-172, D-173).
/// </summary>
[Trait("kind", "integration")]
public sealed class DeploymentDataKeyTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    // The migration before the one that moves the key, where the key stood under the nil
    // subject and the values of no subject were bound to it.
    private const string BeforeTheMove = "20260929142007_MoveValuesUnderTheDeploymentKey";

    private const string TheMove = "20260929170223_HoldTheDeploymentKeyUnderTheMaxUuid";

    private static readonly Guid MaxUuid = Guid.AllBitsSet;

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// PRIV-RIGHT-005a AC18: the key is written under the max UUID the first time a value
    /// needs it, and no row of the subject-key table stands under the nil subject.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_TheDeploymentKeyIsHeldUnderTheMaxUuidAsync()
    {
        byte[] deploymentKey;

        await using (StoreContext writing = database.Context())
        {
            deploymentKey = await _deployment.DataKey(writing).UnwrappedAsync(TestContext.Current.CancellationToken);
        }

        CryptographicOperations.ZeroMemory(deploymentKey);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(MaxUuid, SubjectKeyId.Deployment.Value);
        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.subject_keys WHERE subject = @reserved",
                new { reserved = MaxUuid }));
        Assert.Equal(
            0,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.subject_keys WHERE subject = @nil",
                new { nil = Guid.Empty }));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18 (D-173): the migration moves the key's row from the nil
    /// subject to the max UUID as it is, since its wrap carries no binding to the
    /// identifier, and leaves no row under the nil subject.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_TheMigrationMovesTheKeyToTheMaxUuidAsync()
    {
        string moving = await BeforeTheMoveAsync("moving");
        byte[] wrapped = RandomNumberGenerator.GetBytes(40);

        await using (var connection = new NpgsqlConnection(moving))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
                VALUES (@nil, 1, 1, @wrapped);
                """,
                new { nil = Guid.Empty, wrapped });
        }

        await MigrateAsync(moving, TheMove);

        await using var reading = new NpgsqlConnection(moving);
        await reading.OpenAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<(Guid Subject, byte[] Wrapped)> held =
        [
            .. await reading.QueryAsync<(Guid, byte[])>("SELECT subject, wrapped_key FROM identity.subject_keys"),
        ];

        Assert.Equal([(MaxUuid, wrapped)], held.Select(row => (row.Subject, row.Wrapped)));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18 and AC16 (D-173): a value the field cipher bound before it was
    /// bound to its own row cannot be bound again by the database, which holds no
    /// key-encryption key, so the migration refuses, naming the table, and the key stays
    /// where it stood.
    /// </summary>
    /// <param name="table">The table holding a value bound the old way.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("invitations")]
    [InlineData("mailboxes")]
    [InlineData("send_outbox")]
    [InlineData("registration_sessions")]
    public async Task PRIV_RIGHT_005a_AC18_TheMigrationRefusesNamingTheTableWhereAValueBoundTheOldWayStandsAsync(
        string table)
    {
        string refusing = await BeforeTheMoveAsync("refusing_" + table);

        await using (var connection = new NpgsqlConnection(refusing))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync(
                """
                INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
                VALUES (@nil, 1, 1, @wrapped);
                """,
                new { nil = Guid.Empty, wrapped = RandomNumberGenerator.GetBytes(40) });
            await BoundTheOldWayAsync(connection, table);
        }

        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
            async () => await MigrateAsync(refusing, TheMove));

        Assert.Contains("identity." + table + " ", refusal.MessageText, StringComparison.Ordinal);

        await using var reading = new NpgsqlConnection(refusing);
        await reading.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal([Guid.Empty], await reading.QueryAsync<Guid>("SELECT subject FROM identity.subject_keys"));
    }

    private static async Task MigrateAsync(string connectionString, string target)
    {
        await using StoreContext context = DatabaseFixture.Context(connectionString);
        await context.GetService<IMigrator>().MigrateAsync(target, TestContext.Current.CancellationToken);
    }

    // A row whose value the field cipher bound to the nil subject, or, for a registration
    // session, to its provisional subject. What the value is does not matter: the
    // database cannot tell how it was bound, only that it stands.
    private static async Task BoundTheOldWayAsync(NpgsqlConnection connection, string table)
    {
        switch (table)
        {
            case "invitations":
                (SubjectId inviter, OrganizationId organization) = await InviterAsync(connection);
                await connection.ExecuteAsync(
                    """
                    INSERT INTO identity.invitations
                        (id, organization, inviter, token, wrapped_key, enc_identifiers, roles, documents, issued_at, expires_at)
                    VALUES
                        (gen_random_uuid(), @organization, @inviter, '\x01', '\x02', '\x03', '{}', '[]', now(), now() + interval '1 day');
                    """,
                    new { organization = organization.Value, inviter = inviter.Value });
                break;

            case "mailboxes":
                await connection.ExecuteAsync(
                    """
                    INSERT INTO identity.mailboxes
                        (id, fingerprint, canonicalisation_version, enc_canonical, wrapped_key, reserved_at, attempts, fingerprint_version)
                    VALUES
                        (gen_random_uuid(), '\x01', '16.0.0', '\x02', '\x03', now(), 0, 1);
                    """);
                break;

            case "send_outbox":
                await connection.ExecuteAsync(
                    """
                    INSERT INTO identity.send_outbox (id, recorded_at, wrapped_key, enc_message)
                    VALUES (gen_random_uuid(), now(), '\x01', '\x02');
                    """);
                break;

            default:
                await connection.ExecuteAsync(
                    """
                    INSERT INTO identity.registration_sessions (id, provisional_subject, expires_at, wrapped_key, enc_session)
                    VALUES (gen_random_uuid(), gen_random_uuid(), now() + interval '1 hour', '\x01', '\x02');
                    """);
                break;
        }
    }

    // The database stands at a migration before today's model, so the rows are written
    // in the columns that migration has rather than through the model.
    private static async Task<(SubjectId Inviter, OrganizationId Organization)> InviterAsync(NpgsqlConnection connection)
    {
        SubjectId inviter = Subjects.New();
        var organization = new OrganizationId(Guid.CreateVersion7());

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.accounts (subject, state, created_at)
            VALUES (@inviter, 'active', @at);
            INSERT INTO identity.organizations (id, name, canonical_name, created_at)
            VALUES (@organization, @name, lower(@name), @at);
            """,
            new
            {
                inviter = inviter.Value,
                organization = organization.Value,
                name = "Organization " + organization.Value.ToString("n", CultureInfo.InvariantCulture),
                at = DateTimeOffset.UnixEpoch,
            });

        return (inviter, organization);
    }

    private async Task<string> BeforeTheMoveAsync(string name)
    {
        string connectionString = await database.CreateDatabaseAsync(name);

        await MigrateAsync(connectionString, BeforeTheMove);

        return connectionString;
    }
}
