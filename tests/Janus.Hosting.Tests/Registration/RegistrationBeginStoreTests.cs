using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Registration;

/// <summary>
/// The begin of a registration over the store, in a deployment that declared no
/// challenge verifier: each session created is counted against its source in the
/// transaction that creates it, and the signal that count brings is recorded and the
/// session still created (AUTH-ABUSE-008).
/// </summary>
/// <param name="host">The deployment the test runs against.</param>
[Trait("kind", "integration")]
public sealed class RegistrationBeginStoreTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Whole = "2001:db8:7:7::1";
    private const string Source = "2001:db8:7:7::/64";

    /// <summary>
    /// AUTH-ABUSE-008 AC3 and AC4: the session that makes more from one source in an hour
    /// than <c>abuse.botdefence.repeatedattempts</c> fires the signal; with no verifier
    /// declared it is recorded as <c>auth.botdefence.signalled</c> and the session is
    /// created, and every session created is counted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_EachSessionCreatedIsCountedAndTheSignalItBringsIsRecordedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int admitted = Settings.AbuseBotDefenceRepeatedAttempts.Default;

        for (int begun = 0; begun < admitted; begun++)
        {
            await BeginAsync(cancellationToken);
        }

        Assert.Equal(0, await SignalledAsync());

        await BeginAsync(cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(1, await SignalledAsync());
        Assert.Equal(
            admitted + 1,
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.registration_sources"));
        Assert.Equal(
            admitted + 1,
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.registration_sessions"));
    }

    private async Task BeginAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<IRegistration>()
                .BeginAsync(
                    signedIn: null,
                    "web",
                    "en",
                    Whole,
                    Source,
                    invitationToken: null,
                    challengeToken: null,
                    cancellationToken))
            .Switch(
                _ => { },
                error => throw new Xunit.Sdk.XunitException($"The begin was refused: {error.Code}."));
    }

    private async Task<int> SignalledAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM identity.audit_records WHERE action = @Action",
            new { Action = AuditActions.BotDefenceSignalled.ToString() });
    }
}
