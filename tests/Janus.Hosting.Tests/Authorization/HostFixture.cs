using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Storage.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A deployment as a host runs it: the library's schema, the host's own table beside
/// it, and the library registered over both through its one entry point.
/// </summary>
/// <remarks>
/// Implements CONV-TEST-002. One container per test class, torn down with the class.
/// </remarks>
public sealed class HostFixture : IAsyncLifetime
{
    private readonly DatabaseFixture _database = new();

    private ServiceProvider? _services;

    /// <summary>
    /// How to reach the database both the library and the host read.
    /// </summary>
    public string ConnectionString => _database.ConnectionString;

    /// <summary>
    /// The container the library's services are resolved from.
    /// </summary>
    public IServiceProvider Services => _services
        ?? throw new InvalidOperationException("The fixture has not started.");

    /// <summary>
    /// Opens a connection to the database for a hand-written statement.
    /// </summary>
    /// <returns>The open connection.</returns>
    public async ValueTask<NpgsqlConnection> OpenAsync() => await _database.OpenAsync();

    /// <summary>
    /// Opens a context over the host's own tables, with the library's two contract
    /// tables mapped into it.
    /// </summary>
    /// <returns>The context.</returns>
    internal HostContext Context() =>
        new(new DbContextOptionsBuilder<HostContext>().UseNpgsql(ConnectionString).Options);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        await using (NpgsqlConnection connection = await _database.OpenAsync())
        {
            // LIB-HOST-001 and PRIV-RET-001: the deployment names the keys the
            // library cannot guess, a retention period for each category its purposes
            // declare among them, and the check that runs before anything is served
            // reads them.
            await connection.ExecuteAsync(
                """
                INSERT INTO janus.settings (key, value)
                VALUES
                    ('notification.languages', '["en"]'),
                    ('retention.identity', 'P7Y'),
                    ('retention.history', 'P2Y');

                CREATE SCHEMA host;
                CREATE TABLE host.documents (id text PRIMARY KEY, title text NOT NULL);
                CREATE TABLE host.reviewers (
                    workspace_id text NOT NULL,
                    reviewer uuid NOT NULL,
                    PRIMARY KEY (workspace_id, reviewer));
                CREATE INDEX ix_reviewers_reviewer ON host.reviewers (reviewer);
                """);
        }

        // The host writes no personal field, so the key is never used to wrap one; it
        // is the length a key-encryption key is, and nothing else.
        byte[] material = new byte[32];
        RandomNumberGenerator.Fill(material);

        var services = new ServiceCollection();

        // The cases state when they are evaluated, so the clock does not move under them.
        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));

        services.AddJanus(
            ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = material }),
            Encoding.UTF8.GetBytes("the fingerprint key of this deployment"),
            Declaration(),
            JanusApplication.Public);

        // PRIV-RIGHT-005b: the deployment declares its documents sensitive, so it
        // registers what does the host-side work for them.
        services.AddSingleton<ISubjectEventSubscriber>(new HostSubjectEvents());

        _services = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        await _database.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// What this deployment declares about its own domain, which a test registering a
    /// second collection over the same deployment declares in turn.
    /// </summary>
    /// <param name="materialised">
    /// Whether the derivation is precomputed into grant rows rather than evaluated per
    /// request, which is the same deployment after materialisation (AUTHZ-TEST-001 AC3).
    /// </param>
    /// <returns>The declaration.</returns>
    internal static AuthorizationDeclaration Declaration(bool materialised = false) =>
        new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration(
                "contract",
                IsConsent: false,
                RequiresWrittenConsentForSensitive: false,
                RequiresAssessment: false,
                IsObjectable: false))
            .LawfulBasis(new LawfulBasisDeclaration(
                "agreement",
                IsConsent: true,
                RequiresWrittenConsentForSensitive: true,
                RequiresAssessment: false,
                IsObjectable: false))
            .SensitiveCategory("financial")
            .Permission(HostPermissions.Read.ToString())
            .Permission(HostPermissions.Edit.ToString())
            .Permission(HostPermissions.Publish.ToString())
            .Permission(HostPermissions.ReadNote.ToString())
            .Permission(HostPermissions.Recommend.ToString())
            .StepUpGate(HostPermissions.Publish.ToString(), "document:publish")
            .ServesPurpose(HostPermissions.Recommend.ToString(), "recommendations")
            .Relationship<HostReviewer>(
                "reviewer",
                "workspace",
                "host.reviewers",
                row => row.Reviewer,
                "reviewer",
                row => row.WorkspaceId,
                "workspace_id")
            .Resource<HostWorkspace>("workspace", type => type
                .BelongsToOrganization()
                .Derivation("reviewer", "reviewer", materialised)
                .Purpose("running the host", "contract", data: ["identity"], subjects: ["members"]))
            .Resource<HostDocument>("document", type => type
                .ContainedIn("workspace")
                .Sensitive("financial")
                .Purpose("running the host", "contract", data: ["identity"], subjects: ["members"])
                .Purpose(
                    "recommendations",
                    "agreement",
                    data: ["history"],
                    subjects: ["members"]))
            .Resource<HostNote>("note", type => type
                .ContainedIn("workspace")
                .Discloses()
                .Purpose("running the host", "contract", data: ["identity"], subjects: ["members"]))
            .Build();
}
