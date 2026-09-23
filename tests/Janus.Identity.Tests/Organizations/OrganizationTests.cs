using System;
using Janus.Core;
using Janus.Identity.Organizations;
using Xunit;

namespace Janus.Identity.Tests.Organizations;

/// <summary>
/// An organization and the window its deletion runs through (IDN-ORG-001, IDN-ORG-003,
/// IDN-ORG-004, IDN-PRIN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class OrganizationTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Acme =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly TimeSpan Window = TimeSpan.FromDays(30);

    /// <summary>
    /// IDN-ORG-003: requesting deletion suspends the organization at once, which is
    /// what stops member access.
    /// </summary>
    [Fact]
    public void IDN_ORG_003_ADeletionRequestSuspendsTheOrganizationAtOnce()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        Assert.False(organization.IsSuspended);

        organization.RequestDeletion(Noon);

        Assert.True(organization.IsSuspended);
        Assert.Equal(Noon, organization.DeletionRequestedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC2: a cancellation inside the window restores the organization; it
    /// is not suspended and no erasure is recorded.
    /// </summary>
    [Fact]
    public void IDN_ORG_003_AC2_CancellingInsideTheWindowRestoresTheOrganization()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);

        organization.CancelDeletion();

        Assert.False(organization.IsSuspended);
        Assert.Null(organization.DeletionRequestedAt);
        Assert.Null(organization.ErasedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC3: the erasure does not execute before the configured window has
    /// elapsed, not even one tick before.
    /// </summary>
    [Fact]
    public void IDN_ORG_003_AC3_TheErasureDoesNotExecuteBeforeTheWindowElapses()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);

        Assert.Throws<InvalidOperationException>(() =>
            organization.RecordErasure(Noon + Window - TimeSpan.FromTicks(1), Window));

        Assert.Null(organization.ErasedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC4: the window is the one the caller passes, so a deployment that
    /// has shortened or lengthened the configured grace changes when the erasure may
    /// run, with no deploy.
    /// </summary>
    [Fact]
    public void IDN_ORG_003_AC4_TheWindowIsTheConfiguredOne()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);

        var shorter = TimeSpan.FromDays(7);
        organization.RecordErasure(Noon + shorter, shorter);

        Assert.Equal(Noon + shorter, organization.ErasedAt);
    }

    /// <summary>
    /// IDN-ORG-003 AC5, IDN-PRIN-003: after the erasure the organization is still
    /// there, under the identifier it always had.
    /// </summary>
    [Fact]
    public void IDN_ORG_003_AC5_TheOrganizationSurvivesItsErasure()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);

        organization.RecordErasure(Noon + Window, Window);

        Assert.Equal(Acme, organization.Id);
        Assert.Equal(Noon + Window, organization.ErasedAt);
    }

    /// <summary>
    /// An erasure that has executed closes the window: nothing cancels it and no second
    /// deletion is requested.
    /// </summary>
    [Fact]
    public void CancelDeletion_AfterTheErasure_Throws()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);
        organization.RecordErasure(Noon + Window, Window);

        Assert.Throws<InvalidOperationException>(organization.CancelDeletion);
        Assert.Throws<InvalidOperationException>(() => organization.RequestDeletion(Noon + Window));
    }

    /// <summary>
    /// A window that is not running is neither cancelled nor erased.
    /// </summary>
    [Fact]
    public void CancelDeletion_WithNoWindowRunning_Throws()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);

        Assert.Throws<InvalidOperationException>(organization.CancelDeletion);
        Assert.Throws<InvalidOperationException>(() =>
            organization.RecordErasure(Noon + Window, Window));
    }

    /// <summary>
    /// A second deletion request while a window is running is a fault in the caller,
    /// because it would move the instant access stopped.
    /// </summary>
    [Fact]
    public void RequestDeletion_WhileAWindowRuns_Throws()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);
        organization.RequestDeletion(Noon);

        Assert.Throws<InvalidOperationException>(() =>
            organization.RequestDeletion(Noon + TimeSpan.FromDays(1)));

        Assert.Equal(Noon, organization.DeletionRequestedAt);
    }

    /// <summary>
    /// An organization is called something.
    /// </summary>
    [Fact]
    public void Create_WithoutAName_Throws() =>
        Assert.Throws<ArgumentException>(() => Organization.Create(Acme, "  ", Noon));

    /// <summary>
    /// IDN-ORG-004 AC1: a deletion request naming the administrative organization is
    /// refused by its named code, and no window starts.
    /// </summary>
    [Fact]
    public void IDN_ORG_004_AC1_ADeletionRequestOnTheAdministrativeOrganizationIsRefused()
    {
        var organization = Organization.CreateAdministrative(Acme, "Administration", Noon);

        Result outcome = organization.RequestDeletion(Noon);

        Assert.Equal(ErrorCodes.OrganizationProtected, Code(outcome));
        Assert.False(organization.IsSuspended);
        Assert.Null(organization.DeletionRequestedAt);
    }

    /// <summary>
    /// IDN-ORG-004 AC2: the refusal is the organization's own, so it stands wherever
    /// the request came from. Nothing an application path reaches makes an
    /// organization administrative: the mark is set when bootstrap creates it and is
    /// carried back from the row, and no other path produces one.
    /// </summary>
    [Fact]
    public void IDN_ORG_004_AC2_TheMarkIsSetWhereBootstrapSetsItAndNowhereElse()
    {
        Assert.False(Organization.Create(Acme, "Acme", Noon).IsAdministrative);
        Assert.True(Organization.CreateAdministrative(Acme, "Administration", Noon).IsAdministrative);
        Assert.True(Organization
            .Existing(Acme, "Administration", Noon, isAdministrative: true, null, null)
            .IsAdministrative);
    }

    /// <summary>
    /// An ordinary organization takes the window, which is what IDN-ORG-004 refuses
    /// only the administrative one.
    /// </summary>
    [Fact]
    public void RequestDeletion_AnOrdinaryOrganization_Succeeds()
    {
        var organization = Organization.Create(Acme, "Acme", Noon);

        Result outcome = organization.RequestDeletion(Noon);

        Assert.Null(Code(outcome));
        Assert.True(organization.IsSuspended);
    }

    private static ErrorCode? Code(Result outcome)
    {
        ErrorCode? code = null;

        outcome.Switch(() => { }, failure => code = failure.Code);

        return code;
    }
}
