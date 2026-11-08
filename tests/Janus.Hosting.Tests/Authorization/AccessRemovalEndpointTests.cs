using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's check of a removal of access answered over <c>DELETE /admin/grants/{id}</c>:
/// its own code as 409, a check that throws as 503 and a code under a library prefix as a
/// fault (AUTHZ-GRANT-005, D-281).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccessRemovalEndpointTests : IAsyncLifetime
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly RoleName Reader = RoleName.Parse("reader");

    private static readonly Guid Holder = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private static readonly ErrorCode Kept = ErrorCode.Parse("workshop.lastreader");

    private readonly RemovalAnswers _answers = new();

    private readonly Janus.Hosting.Tests.Deployment _deployment;

    /// <summary>
    /// A deployment whose host registered a check, administered by one organization and
    /// holding a branch.
    /// </summary>
    public AccessRemovalEndpointTests()
    {
        _deployment = new Janus.Hosting.Tests.Deployment(
            services: services => services.AddSingleton<IAccessRemovalCheck>(_answers));
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
        _deployment.Organizations.Seed(Branch);
    }

    /// <summary>
    /// The role the tests hand out.
    /// </summary>
    /// <returns>The work of writing it.</returns>
    public async ValueTask InitializeAsync() =>
        await _deployment.Roles.CreateAsync(Role.Of(Reader, [Permission.Parse("document:read")]), CancellationToken.None);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a revocation the host's check refuses answers 409 with the
    /// host's code and details, leaves the grant live, and is recorded naming the kind, the
    /// code and the path of the library's own route; the check is told the grant, its role
    /// and the reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ARevocationTheHostRefusesAnswersItsCodeAs409Async()
    {
        Browser administrator = await AuthorisedAsync();
        Grant written = await WrittenAsync();

        _answers.Answer = _ => Result.Failure(Error.From(Kept, "role", System.Text.Json.JsonSerializer.SerializeToElement("reader")));

        Answer refused = await RevokedAsync(administrator, written);

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(Kept.ToString(), refused.Text("code"));
        Assert.Equal("reader", refused.Json().GetProperty("details").GetProperty("role").GetString());
        Assert.Null((await _deployment.AccessGrants.FindAsync(written.Id, CancellationToken.None))!.RevokedAt);

        AccessRemoval asked = Assert.Single(_answers.Asked);
        (AccessRemoval removal, ErrorCode code) = Assert.Single(_deployment.GrantChanges.Refusals);

        Assert.Equal(
            (AccessRemovalKind.GrantRevoked, Branch, (GrantId?)written.Id, (RoleName?)Reader, "Left the project.", AccessRemovalPath.Administration),
            (asked.Kind, asked.Organization, asked.Grant, asked.Role, asked.Reason, asked.Path));
        Assert.Equal((AccessRemovalKind.GrantRevoked, Kept, AccessRemovalPath.Administration), (removal.Kind, code, removal.Path));
        Assert.DoesNotContain(_deployment.GrantChanges.Changes, change => change.Action == AuditActions.GrantRevoked);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a check that throws refuses the revocation with 503
    /// <c>authz.removal.uncheckable</c>, never letting it through, and the grant stays live.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ACheckThatThrowsRefusesWith503Async()
    {
        Browser administrator = await AuthorisedAsync();
        Grant written = await WrittenAsync();

        _answers.Answer = _ => throw new TimeoutException("The host's own store did not answer.");

        Answer refused = await RevokedAsync(administrator, written);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, refused.Status);
        Assert.Equal(ErrorCodes.RemovalUncheckable.ToString(), refused.Text("code"));
        Assert.Null((await _deployment.AccessGrants.FindAsync(written.Id, CancellationToken.None))!.RevokedAt);
        Assert.Equal(ErrorCodes.RemovalUncheckable, Assert.Single(_deployment.GrantChanges.Refusals).Code);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a code under one of the library's prefixes, or
    /// <c>session.</c>, is the host's fault: the revocation answers 500
    /// <c>system.fault</c> and the grant stays live.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_AHostCodeUnderALibraryPrefixAnswersAFaultAsync()
    {
        Browser administrator = await AuthorisedAsync();

        foreach (string code in new[] { "authz.grant.kept", "session.kept" })
        {
            Grant written = await WrittenAsync();

            _answers.Answer = _ => Result.Failure(Error.From(ErrorCode.Parse(code)));

            Answer refused = await RevokedAsync(administrator, written);

            Assert.Equal(StatusCodes.Status500InternalServerError, refused.Status);
            Assert.Equal(ErrorCodes.SystemFault.ToString(), refused.Text("code"));
            Assert.Null((await _deployment.AccessGrants.FindAsync(written.Id, CancellationToken.None))!.RevokedAt);
        }

        Assert.Empty(_deployment.GrantChanges.Refusals);
    }

    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): a deny's revocation widens access, so the check is not
    /// asked about it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_ADenysRevocationIsNotCheckedAsync()
    {
        Browser administrator = await AuthorisedAsync();
        Grant written = await WrittenAsync(deny: true);

        _answers.Answer = _ => Result.Failure(Error.From(Kept));

        Answer revoked = await RevokedAsync(administrator, written);

        Assert.Equal(StatusCodes.Status204NoContent, revoked.Status);
        Assert.Empty(_answers.Asked);
    }

    private static Task<Answer> RevokedAsync(Browser administrator, Grant grant) =>
        administrator.SendAsync("DELETE", "/admin/grants/" + grant.Id, ("reason", "Left the project."));

    // A grant of the branch to the holder, written as an administrator would have.
    private async Task<Grant> WrittenAsync(bool deny = false)
    {
        Grant written = Grant
            .Create(
                GrantId.New(_deployment.Clock),
                new GrantSubject(SubjectType.User, Holder),
                Reader,
                Branch,
                on: null,
                deny,
                GrantKind.Stored,
                expiresAt: null,
                new SubjectId(Holder),
                _deployment.Clock.GetUtcNow(),
                "Needs it.")
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));

        await _deployment.AccessGrants.CreateAsync(written, CancellationToken.None);

        return written;
    }

    // A signed-in administrator managing the branch's grants, handing out the role.
    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Gate.Grant(subject, Branch, Permissions.GrantManage);
        _deployment.HandOuts.HandsOut(subject, Branch, Reader);

        return browser;
    }

    // The host's check, answering as the case sets it and keeping what it was asked.
    private sealed class RemovalAnswers : IAccessRemovalCheck
    {
        public Func<AccessRemoval, Result> Answer { get; set; } = _ => Result.Success();

        public System.Collections.Generic.List<AccessRemoval> Asked { get; } = [];

        public ValueTask<Result> CheckAsync(AccessRemoval removal, CancellationToken cancellationToken)
        {
            Asked.Add(removal);

            return ValueTask.FromResult(Answer(removal));
        }
    }
}
