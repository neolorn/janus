using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Audit;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What a refused factor leaves in the trail: the factor, the account where there was
/// one, and nothing that was typed (CONV-LOG-005, IDN-AUD-001).
/// </summary>
/// <remarks>
/// The port implementation is tested against the real database (D-156), which is what
/// holds the trail to its identity constraints. One database serves the class, so each
/// test writes an account of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class SessionAuditTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// A factor refused against an account is a security record naming the account as
    /// the one the attempt was made against, no actor, and the factor alone.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task FailedAsync_AgainstAnAccount_NamesTheAccountAndTheFactorAloneAsync()
    {
        SubjectId account = await _deployment.AccountAsync(DateTimeOffset.UtcNow);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).FailedAsync(
                account,
                Factor.Password,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await OfAsync(account));
        KeyValuePair<string, JsonElement> field = Assert.Single(read.Details);

        Assert.Equal(AuditActions.AuthenticationFailed, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(default, read.ActingSubject);
        Assert.Equal(default, read.EffectiveSubject);
        Assert.Equal(account, read.Subject);
        Assert.Null(read.Organization);
        Assert.Equal("factor", field.Key);
        Assert.Equal("password", field.Value.GetString());
        Assert.Empty(read.PersonalDetails);
    }

    /// <summary>
    /// A factor refused against an identifier no account holds is taken by the
    /// database, naming no account, which is the recorded fact.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task FailedAsync_AgainstNoAccount_IsTakenNamingNoAccountAsync()
    {
        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).FailedAsync(
                subject: null,
                Factor.BreakGlass,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        (Guid acting, Guid effective, string factor) = await connection.QuerySingleAsync<(Guid, Guid, string)>(
            """
            SELECT acting_subject, effective_subject, details ->> 'factor'
            FROM identity.audit_records
            WHERE action = 'auth.authentication.failed' AND subject IS NULL;
            """);

        Assert.Equal((Guid.Empty, Guid.Empty, "breakGlass"), (acting, effective, factor));
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a refused code of the new-device check is an authentication
    /// failure against the account whose sign-in it would complete, no actor, and
    /// details of exactly the verification, naming no factor.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_LOG_005_AC1_ARefusedDeviceCodeIsRecordedAsItsVerificationAsync()
    {
        SubjectId account = await _deployment.AccountAsync(DateTimeOffset.UtcNow);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).DeviceVerificationFailedAsync(
                account,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await OfAsync(account));
        KeyValuePair<string, JsonElement> field = Assert.Single(read.Details);

        Assert.Equal(AuditActions.AuthenticationFailed, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(default, read.ActingSubject);
        Assert.Equal(default, read.EffectiveSubject);
        Assert.Equal(account, read.Subject);
        Assert.Equal("verification", field.Key);
        Assert.Equal("device", field.Value.GetString());
        Assert.Empty(read.PersonalDetails);
    }

    /// <summary>
    /// A factor refused at a step-up is a security record of the account against the
    /// session it was presented on.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task StepUpFailedAsync_NamesTheAccountAndTheSessionAsync()
    {
        SubjectId account = await _deployment.AccountAsync(DateTimeOffset.UtcNow);
        var session = new SessionId(Guid.CreateVersion7());

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).StepUpFailedAsync(
                session,
                account,
                breakGlassReason: null,
                Factor.Totp,
                DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await OfAsync(account));

        Assert.Equal(AuditActions.StepUpFailed, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(account, read.ActingSubject);
        Assert.Equal(account, read.EffectiveSubject);
        Assert.Equal(account, read.Subject);
        Assert.Equal(session.ToString(), read.Details["session"].GetString());
        Assert.Equal("totp", read.Details["factor"].GetString());
    }

    private AuditStore Store(StoreContext context) =>
        new(context, new DataConnections(context), _deployment.Ring, _deployment.Randomness);

    private SessionAudit Audit(StoreContext context) => new(Store(context), TimeProvider.System);

    // What the trail holds with the subject as the account it concerns.
    private async Task<IReadOnlyList<AuditRecord>> OfAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken);
    }
}
