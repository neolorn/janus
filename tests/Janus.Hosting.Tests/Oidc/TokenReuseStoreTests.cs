using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Janus.Storage.Authentication.Oidc;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// A refresh token's reuse over the database: the session record's spine, the
/// provider's grant and every token of it revoked, with the revocation's audit record,
/// in one commit, and none of them where the commit fails, whatever the token names
/// (AUTH-OIDC-003, D-207, D-208).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class TokenReuseStoreTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Client = "reuse-client";

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-207): a reuse revokes the spine's sessions, the grant and its
    /// tokens and writes the revocation's audit record in one commit, and leaves the
    /// tokens of another grant as they stood.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseRevokesTheFamilyAndItsRecordInOneCommitAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, SessionId session, Guid grant, Guid other) = await FamilyAsync(cancellationToken);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OidcService>()
                .ReuseAsync(subject, session, Client, grant.ToString(), cancellationToken);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.True(await EndedAsync(connection, session));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await GrantStatusAsync(connection, grant));
        Assert.Equal([OpenIddictConstants.Statuses.Revoked], await TokenStatusesAsync(connection, grant));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await GrantStatusAsync(connection, other));
        Assert.Equal([OpenIddictConstants.Statuses.Valid], await TokenStatusesAsync(connection, other));
        Assert.Equal(1, await RecordedAsync(connection, subject));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-207): where the reuse's commit fails, none of the family is
    /// revoked and nothing is recorded, so the next presentation revokes them again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_WhereTheCommitFailsNoneOfTheFamilyIsRevokedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, SessionId session, Guid grant, _) = await FamilyAsync(cancellationToken);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            OidcService refusing = ActivatorUtilities.CreateInstance<OidcService>(
                scope.ServiceProvider,
                new UnitOfWorkRefusingCommit(scope.ServiceProvider.GetRequiredService<IUnitOfWork>()));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await refusing.ReuseAsync(subject, session, Client, grant.ToString(), cancellationToken));
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.False(await EndedAsync(connection, session));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await GrantStatusAsync(connection, grant));
        Assert.Equal(
            [OpenIddictConstants.Statuses.Redeemed, OpenIddictConstants.Statuses.Valid],
            await TokenStatusesAsync(connection, grant));
        Assert.Equal(0, await RecordedAsync(connection, subject));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-208): a reuse of a redeemed token naming no session and no
    /// subject revokes its grant and every token of it, ends no session, and writes the
    /// record under the grant's subject with no session, in one commit.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseNamingNoSessionAndNoSubjectRevokesItsGrantUnderTheGrantsSubjectAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, SessionId session, Guid grant, Guid other) = await FamilyAsync(cancellationToken);
        string presenting = Presenting();

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OidcService>()
                .ReuseAsync(subject: null, session: null, presenting, grant.ToString(), cancellationToken);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.False(await EndedAsync(connection, session));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await GrantStatusAsync(connection, grant));
        Assert.Equal([OpenIddictConstants.Statuses.Revoked], await TokenStatusesAsync(connection, grant));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await GrantStatusAsync(connection, other));
        Assert.Equal(
            (subject.Value, subject.Value, (Guid?)subject.Value, false),
            await RecordOfAsync(connection, presenting));
    }

    /// <summary>
    /// AUTH-OIDC-003 AC4 (D-208): a reuse of a redeemed token issued under no grant and
    /// naming no session and no subject revokes no grant and ends no session, and writes
    /// the record under the nil subject with no subject and no session, in one commit.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC4_AReuseUnderNoGrantNamingNoSessionAndNoSubjectIsRecordedUnderTheNilSubjectAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (_, SessionId session, Guid grant, Guid other) = await FamilyAsync(cancellationToken);
        string presenting = Presenting();

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OidcService>()
                .ReuseAsync(subject: null, session: null, presenting, grant: null, cancellationToken);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.False(await EndedAsync(connection, session));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await GrantStatusAsync(connection, grant));
        Assert.Equal(
            [OpenIddictConstants.Statuses.Redeemed, OpenIddictConstants.Statuses.Valid],
            await TokenStatusesAsync(connection, grant));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await GrantStatusAsync(connection, other));
        Assert.Equal((Guid.Empty, Guid.Empty, (Guid?)null, false), await RecordOfAsync(connection, presenting));
    }

    // A client name of the test's own, so the one record it writes is found by it.
    private static string Presenting() => $"{Client}-{Guid.NewGuid():N}";

    private static async Task<(Guid Acting, Guid Effective, Guid? Subject, bool Session)> RecordOfAsync(
        NpgsqlConnection connection,
        string client) =>
        await connection.QuerySingleAsync<(Guid, Guid, Guid?, bool)>(
            """
            SELECT acting_subject, effective_subject, subject, details ->> 'session' IS NOT NULL
            FROM identity.audit_records
            WHERE action = @action AND details ->> 'client' = @client
            """,
            new { action = AuditActions.RefreshTokenReused.ToString(), client });

    private static async Task<bool> EndedAsync(NpgsqlConnection connection, SessionId session) =>
        await connection.ExecuteScalarAsync<bool>(
            "SELECT bool_and(ended_at IS NOT NULL) FROM identity.sessions WHERE spine = @spine",
            new { spine = session.Value });

    private static async Task<string> GrantStatusAsync(NpgsqlConnection connection, Guid grant) =>
        (await connection.ExecuteScalarAsync<string>(
            "SELECT status FROM identity.oidc_authorizations WHERE id = @grant",
            new { grant }))!;

    private static async Task<string[]> TokenStatusesAsync(NpgsqlConnection connection, Guid grant) =>
        [.. await connection.QueryAsync<string>(
            "SELECT DISTINCT status FROM identity.oidc_tokens WHERE authorization_id = @grant ORDER BY status",
            new { grant })];

    private static async Task<int> RecordedAsync(NpgsqlConnection connection, SubjectId subject) =>
        await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM identity.audit_records WHERE action = @action AND subject = @subject",
            new { action = AuditActions.RefreshTokenReused.ToString(), subject = subject.Value });

    // An account signed in once, its record's spine holding a grant of the client with
    // a redeemed refresh token and a live one, and a second grant with a token of its own.
    private async Task<(SubjectId Subject, SessionId Session, Guid Grant, Guid Other)> FamilyAsync(
        CancellationToken cancellationToken)
    {
        SubjectId subject = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        var session = SessionId.New(TimeProvider.System);

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IServiceProvider provider = scope.ServiceProvider;
            IUnitOfWork work = provider.GetRequiredService<IUnitOfWork>();
            IOidcClientStore clients = provider.GetRequiredService<IOidcClientStore>();
            using var randomness = RandomNumberGenerator.Create();

            Assert.True((await work.BeginAsync(cancellationToken)).Match(_ => true, _ => false));

            if (await clients.FindAsync(Client, cancellationToken) is null)
            {
                await clients.AddAsync(
                    new OidcClient(
                        Client,
                        "The reuse client",
                        OidcClientKind.Protocol,
                        "https://mail.example.test/signin/callback",
                        ["openid", "offline_access"]),
                    Encoding.UTF8.GetBytes("a-secret-the-deployment-set"),
                    Authorization.Deployment.Noon,
                    cancellationToken);
            }

            await provider.GetRequiredService<ISessionStore>().AddAsync(
                Session.Begin(
                    session,
                    subject,
                    new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                    new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                    Authorization.Deployment.Noon,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromDays(30),
                    breakGlassReason: null),
                OpaqueToken.Draw(randomness).Fingerprint(),
                OpaqueToken.Draw(randomness).Fingerprint(),
                cancellationToken);

            Assert.True((await work.CommitAsync(cancellationToken)).Match(() => true, _ => false));
        }

        Guid grant = await GrantedAsync(subject, cancellationToken);
        Guid other = await GrantedAsync(subject, cancellationToken);

        await TokenAsync(subject, grant, OpenIddictConstants.Statuses.Redeemed, cancellationToken);
        await TokenAsync(subject, grant, OpenIddictConstants.Statuses.Valid, cancellationToken);
        await TokenAsync(subject, other, OpenIddictConstants.Statuses.Valid, cancellationToken);

        return (subject, session, grant, other);
    }

    private async Task<Guid> GrantedAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IOpenIddictAuthorizationStore<OidcAuthorizationRecord> authorizations =
            scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationStore<OidcAuthorizationRecord>>();
        OidcAuthorizationRecord grant = await authorizations.InstantiateAsync(cancellationToken);

        await authorizations.SetApplicationIdAsync(grant, Client, cancellationToken);
        await authorizations.SetSubjectAsync(grant, subject.ToString(), cancellationToken);
        await authorizations.SetStatusAsync(grant, OpenIddictConstants.Statuses.Valid, cancellationToken);
        await authorizations.SetTypeAsync(grant, OpenIddictConstants.AuthorizationTypes.AdHoc, cancellationToken);
        await authorizations.SetScopesAsync(grant, ["openid", "offline_access"], cancellationToken);
        await authorizations.SetCreationDateAsync(grant, Authorization.Deployment.Noon, cancellationToken);
        await authorizations.CreateAsync(grant, cancellationToken);

        return grant.Id;
    }

    private async Task TokenAsync(SubjectId subject, Guid grant, string status, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IOpenIddictTokenStore<OidcTokenRecord> tokens =
            scope.ServiceProvider.GetRequiredService<IOpenIddictTokenStore<OidcTokenRecord>>();
        OidcTokenRecord token = await tokens.InstantiateAsync(cancellationToken);

        await tokens.SetApplicationIdAsync(token, Client, cancellationToken);
        await tokens.SetAuthorizationIdAsync(token, grant.ToString(), cancellationToken);
        await tokens.SetSubjectAsync(token, subject.ToString(), cancellationToken);
        await tokens.SetStatusAsync(token, status, cancellationToken);
        await tokens.SetTypeAsync(token, OpenIddictConstants.TokenTypeHints.RefreshToken, cancellationToken);
        await tokens.SetCreationDateAsync(token, Authorization.Deployment.Noon, cancellationToken);
        await tokens.SetExpirationDateAsync(token, Authorization.Deployment.Noon + TimeSpan.FromDays(7), cancellationToken);
        await tokens.CreateAsync(token, cancellationToken);
    }
}
