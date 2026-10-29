using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A host's erasure of a person record it keeps nothing about any longer, read against the
/// deployment's own gate and store (REC-ADM-003, D-223).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class RecordErasureTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// REC-ADM-003 AC1 (D-223): the host's declared job, declaring <c>record:erase</c>,
    /// erases an active record outside a request; the record is <c>deleted</c>, its erasures
    /// row carries <c>retention</c>, and the erasure is recorded with the job its principal,
    /// <c>details.reason</c> <c>retention</c> and the reason it gave in
    /// <c>details.statedReason</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ADM_003_AC1_AJobDeclaringRecordEraseErasesAnActiveRecordAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([], cancellationToken);

        SubjectId clerk = await deployment.AccountAsync(cancellationToken);
        RoleName entering = await deployment.RoleAsync([Permissions.RecordCreate], cancellationToken);

        _ = await deployment.GrantAsync(
            GrantSubject.Of(clerk), entering, null, false, null, deployment.Organization, cancellationToken);

        SubjectId record = await EnteredAsync(clerk, deployment.Organization, cancellationToken);
        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            deployment.Organization));

        Result erased;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            erased = await scope.ServiceProvider.GetRequiredService<IPersonRecords>()
                .EraseAsync(job, record, "No booking is kept under it.", cancellationToken);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        string state = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT state FROM identity.person_records WHERE subject = @record",
            new { record = record.Value },
            cancellationToken: cancellationToken));
        string reason = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT reason FROM identity.erasures WHERE subject = @record",
            new { record = record.Value },
            cancellationToken: cancellationToken));
        (string? principal, string? spelled, string? stated) = await connection
            .QuerySingleAsync<(string?, string?, string?)>(new CommandDefinition(
                """
                SELECT principal, details->>'reason', details->>'statedReason' FROM identity.audit_records
                WHERE action = 'privacy.erasure.executed' AND subject = @record
                """,
                new { record = record.Value },
                cancellationToken: cancellationToken));

        Assert.True(erased.Match(() => true, _ => false));
        Assert.Equal(("deleted", "retention"), (state, reason));
        Assert.Equal(
            (HostFixture.Digest, "retention", "No booking is kept under it."),
            (principal, spelled, stated));
    }

    // A record staff entered in the organization by an account holding record:create there.
    private async Task<SubjectId> EnteredAsync(
        SubjectId clerk,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IPersonRecords>().EnterAsync(
                AccessContext.Of(clerk),
                new PersonRecordEntry(
                    organization,
                    "en",
                    Name: null,
                    Email: null,
                    "+441632960032",
                    Consents: [],
                    DateOfBirth: null,
                    Reference: null),
                cancellationToken))
            .Match(subject => subject, error => throw new InvalidOperationException(error.Code.ToString()));
    }
}
