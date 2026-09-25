using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Breaches;
using Janus.Privacy.Policies;
using Xunit;

namespace Janus.Privacy.Tests.Breaches;

/// <summary>
/// The audit trail read by data subject, for a caller holding <c>audit:read</c>
/// (PRIV-BREACH-002, AUTHZ-CONCEAL-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuditTrailServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly AuditTrailStoreInMemory _trail = new();

    /// <summary>
    /// A deployment administered by one organization, whose trail holds two records of
    /// one customer and one of another.
    /// </summary>
    public AuditTrailServiceTests()
    {
        _administrative.Organization = Company;

        _trail.Hold(Entry(Ahmed, "identity.account.suspended", Noon.AddHours(-2)));
        _trail.Hold(Entry(Ahmed, "identity.account.reactivated", Noon.AddHours(-1)));
        _trail.Hold(Entry(Mona, "identity.account.suspended", Noon));
    }

    private AuditTrailService Trail => new(new AdministrativeScope(_gate, _administrative), _trail);

    /// <summary>
    /// PRIV-BREACH-002: a caller holding <c>audit:read</c> in the administrative
    /// organization reads every record of the one subject, most recent first, and none
    /// of anyone else's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BREACH_002_EveryRecordOfOneSubjectIsReadAsync()
    {
        _gate.Grant(Mona, Company, Permissions.AuditRead);

        Result<IReadOnlyList<AuditEntry>> read = await Trail.OfSubjectAsync(
            AccessContext.Of(Mona),
            Ahmed,
            TestContext.Current.CancellationToken);

        IReadOnlyList<AuditEntry> entries = read.Match(
            value => value,
            error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(
            ["identity.account.reactivated", "identity.account.suspended"],
            entries.Select(entry => entry.Action.ToString()));
        Assert.All(entries, entry => Assert.Equal(Ahmed, entry.Effective));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: without <c>audit:read</c>, or with it held in another
    /// organization, the read is refused as forbidden.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_TheReadIsRefusedWithoutThePermissionAsync()
    {
        _gate.Grant(Ahmed, Branch, Permissions.AuditRead);

        Result<IReadOnlyList<AuditEntry>> withNone = await Trail.OfSubjectAsync(
            AccessContext.Of(Mona),
            Ahmed,
            TestContext.Current.CancellationToken);

        Result<IReadOnlyList<AuditEntry>> elsewhere = await Trail.OfSubjectAsync(
            AccessContext.Of(Ahmed),
            Ahmed,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refusal(withNone).Code);
        Assert.Equal(ErrorCodes.Denied, Refusal(elsewhere).Code);
    }

    private static Error Refusal(Result<IReadOnlyList<AuditEntry>> outcome) => outcome.Match(
        _ => throw new InvalidOperationException("The read was not refused."),
        error => error);

    private static AuditEntry Entry(SubjectId subject, string action, DateTimeOffset at) => new(
        new AuditRecordId(Guid.CreateVersion7()),
        AuditCategory.Security,
        AuditAction.Parse(action),
        at,
        subject,
        subject,
        Organization: null,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));
}
