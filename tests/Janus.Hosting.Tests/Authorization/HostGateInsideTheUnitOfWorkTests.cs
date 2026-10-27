using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Privacy.SubjectKeys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A host's bound action whose gate the host first asks inside its own unit of work: the
/// step-up is judged while a unit of work is open, so it asks no phone signal and records
/// no consideration, and makes its offer as where no signal is declared
/// (CONV-DESIGN-003, AUTH-FACT-002b, D-209, D-210).
/// </summary>
/// <param name="host">The deployment the case runs against, its system policy its own.</param>
[Trait("kind", "integration")]
public sealed class HostGateInsideTheUnitOfWorkTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Number = "+441632960011";

    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    // Whether the carrier was asked at all, which it never is here.
    private bool _asked;

    /// <summary>
    /// CONV-DESIGN-003 AC13 (D-210): a host's bound action whose gate the host first asks
    /// through the access gate inside its own unit of work, on a session whose proof has
    /// aged, is refused <c>auth.stepup.required</c> with the offer made where no signal is
    /// declared, the text code among the combinations; the carrier, failing when called,
    /// is not asked, and no consideration is recorded though the host commits its unit of
    /// work.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC13_AHostsGateUnmetAtItsAskInsideItsUnitOfWorkAsksNoSignalAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId account, ResourceReference record) = await PublishingAsync();

        await using ServiceProvider deployment = Signalled();

        var aged = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: false),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Deployment.Noon - TimeSpan.FromDays(1),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        await HeldAsync(deployment, aged);

        Error refused;

        await using (AsyncServiceScope request = deployment.CreateAsyncScope())
        {
            await using HostContext reading = host.Context();

            IServiceProvider services = request.ServiceProvider;
            IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

            services.GetRequiredService<RequestSession>().Resolved(aged);

            _ = await work.BeginAsync(cancellationToken);

            Result outcome = await services.GetRequiredService<IAccessGate>().RequireAsync(
                AccessContext.Of(account),
                HostPermissions.Publish,
                record,
                new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
                    .Relationship("reviewer", reading.Reviewers),
                cancellationToken);

            _ = await work.CommitAsync(cancellationToken);

            refused = outcome.Match<Error>(
                () => throw new InvalidOperationException("The action was admitted."),
                error => error);
        }

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("present", refused.Details["outcome"].GetString());
        Assert.Contains(
            refused.Details["options"].EnumerateArray(),
            combination => combination.EnumerateArray().Any(factor => factor.GetString() == "phoneCode"));
        Assert.False(_asked);
        Assert.Equal(0, await ConsideredAsync());
    }

    // A record whose publishing the account's grant confers, in an organization the
    // account is a member of and whose policy is the system policy, which lets a text code
    // be presented at a step-up.
    private async Task<(SubjectId Account, ResourceReference Record)> PublishingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName publishing = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Publish], cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var workspace = new ResourceReference(Workspace, ResourceId.Parse(Guid.NewGuid().ToString()));
        var record = new ResourceReference(Document, ResourceId.Parse(Guid.NewGuid().ToString()));

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.MemberAsync(account, deployment.Organization, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(account), publishing, record, false, null, null, cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO identity.settings (key, value) VALUES (@key, @value);",
            new
            {
                key = Settings.PolicyDefault.Key.ToString(),
                value = Settings.PolicyDefault.Write(Policies.SystemDefault with
                {
                    LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
                }),
            },
            cancellationToken: cancellationToken));

        return (account, record);
    }

    // The same deployment with a carrier's signal registered that fails the case when it
    // is called.
    private ServiceProvider Signalled()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddSingleton(new PhoneSignalProvider((_, _) =>
        {
            _asked = true;

            throw new InvalidOperationException("The signal was asked inside a unit of work.");
        }));
        HostFixture.Sourced(services, host.ConnectionString);
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // The account's session, its password and a text code's credential bound to its
    // number, as the store keeps them.
    private static async Task HeldAsync(IServiceProvider deployment, Session session)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        _ = await work.BeginAsync(cancellationToken);

        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(session.Subject, cancellationToken) is null)
        {
            await keys.CreateAsync(session.Subject, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);

        using var randomness = RandomNumberGenerator.Create();

        await services.GetRequiredService<IPasswordStore>().SetAsync(
            Password.Set(
                session.Subject,
                new Argon2idHasher(randomness).Hash(
                    Encoding.UTF8.GetBytes("a horse outstanding in its field"),
                    new Argon2StrengthClass(Settings.PasswordArgon2Memory.Default, Settings.PasswordArgon2Iterations.Default),
                    parallelism: 1),
                meetsSingleFactorFloor: true,
                Deployment.Noon - TimeSpan.FromDays(30)),
            cancellationToken);
        await services.GetRequiredService<IAuthenticatorStore>().AddAsync(
            Authenticator.Bound(
                AuthenticatorId.New(TimeProvider.System),
                session.Subject,
                Factor.PhoneCode,
                CredentialLabel.TryParse("Number", out CredentialLabel label)
                    ? label
                    : throw new InvalidOperationException("The label does not read."),
                Number,
                Deployment.Noon - TimeSpan.FromDays(30)),
            cancellationToken);

        _ = await work.CommitAsync(cancellationToken);
    }

    // Every consideration of a number the deployment's trail holds.
    private async Task<int> ConsideredAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.audit_records WHERE action = @action;",
            new { action = AuditActions.PhoneSignalConsidered.ToString() },
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
