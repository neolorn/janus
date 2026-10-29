using System;
using System.Data.Common;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// The session record as a request reads it in a deployment: from PostgreSQL, at every
/// request, with nothing standing in front of it (AUTH-SESS-001, AUTH-PRIN-001,
/// INF-CACHE-001).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class SessionRecordTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Source = "198.51.100.7";

    // The database every record of the deployment is read from.
    private const string Refusing =
        """
        ALTER DATABASE identity WITH ALLOW_CONNECTIONS false;
        SELECT pg_terminate_backend(pid) FROM pg_stat_activity
        WHERE datname = 'identity' AND pid <> pg_backend_pid();
        """;

    private const string Answering = "ALTER DATABASE identity WITH ALLOW_CONNECTIONS true;";

    /// <summary>
    /// AUTH-SESS-001 AC2: a session ended by a write to its row alone, which no
    /// operation of the library made and nothing in the process was told of, is
    /// refused at the next request that presents its secret.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_SESS_001_AC2_ASessionEndedByAWriteToItsRecordAloneIsRefusedAtItsNextRequestAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId account = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        (SessionId session, OpaqueToken secret) = await BegunAsync(account, cancellationToken);

        ErrorCode? before = await ResolvedAsync(secret, cancellationToken);

        await using (NpgsqlConnection connection = await host.OpenAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE identity.sessions SET ended_at = @at WHERE id = @id;",
                new { at = Authorization.Deployment.Noon, id = session.Value },
                cancellationToken: cancellationToken));
        }

        ErrorCode? after = await ResolvedAsync(secret, cancellationToken);

        Assert.Null(before);
        Assert.Equal(ErrorCodes.SessionExpired, after);
    }

    /// <summary>
    /// AUTH-PRIN-001 AC1: while PostgreSQL cannot answer, a request presenting the
    /// secret of a live session resolves no session: the read fails as a fault, which
    /// the request is answered with, and nothing stands in for the record. Once
    /// PostgreSQL answers again the same secret resolves, so it was the outage that
    /// denied.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_PRIN_001_AC1_WhilePostgreSqlCannotAnswerNoSessionResolvesAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId account = await new Authorization.Deployment(host).AccountAsync(cancellationToken);
        (SessionId _, OpaqueToken secret) = await BegunAsync(account, cancellationToken);

        ErrorCode? before = await ResolvedAsync(secret, cancellationToken);

        await AdministerAsync(Refusing, cancellationToken);

        try
        {
            // The first request meets a connection the outage cut, and the second one
            // PostgreSQL refuses to open.
            for (int request = 0; request < 2; request++)
            {
                Exception fault = await Assert.ThrowsAnyAsync<Exception>(
                    async () => await ResolvedAsync(secret, cancellationToken));

                _ = Assert.IsAssignableFrom<DbException>(fault as DbException ?? fault.InnerException);
            }
        }
        finally
        {
            await AdministerAsync(Answering, cancellationToken);
        }

        Assert.Null(before);
        Assert.Null(await ResolvedAsync(secret, cancellationToken));
    }

    // What a request presenting the secret is answered by the stage that establishes
    // who is asking, in a scope of its own as each request has: nothing where the
    // session resolves, or the refusal's code.
    private async Task<ErrorCode?> ResolvedAsync(OpaqueToken secret, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<SessionService>()
                .ResolveAsync(
                    secret,
                    new SessionOrigin(Source, new DeviceDescription("Firefox", "Linux")),
                    cancellationToken))
            .Match<ErrorCode?>(_ => null, error => error.Code);
    }

    // A live session of the account, written as a sign-in writes it, and the secret
    // its browser carries.
    private async Task<(SessionId Session, OpaqueToken Secret)> BegunAsync(
        SubjectId account,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
            new SessionOrigin(Source, new DeviceDescription("Firefox", "Linux")),
            services.GetRequiredService<TimeProvider>().GetUtcNow(),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        using var randomness = RandomNumberGenerator.Create();
        var secret = OpaqueToken.Draw(randomness);
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            secret.Fingerprint(),
            OpaqueToken.Draw(randomness).Fingerprint(),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return (session.Id, secret);
    }

    // A statement of the instance's administrator, sent over a connection to its
    // maintenance database, which stays reachable while the library's own is not.
    private async Task AdministerAsync(string statement, CancellationToken cancellationToken)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(host.ConnectionString)
        {
            Database = "postgres",
            Pooling = false,
        };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);

        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
    }
}
