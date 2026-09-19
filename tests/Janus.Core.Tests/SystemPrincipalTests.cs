using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// What a background job, an import or a webhook acts as (IDN-PRIN-001).
/// </summary>
/// <remarks>
/// The rule the two scopes exist to stop is "no user, therefore allow", which once
/// written is a hole no code review catches because it looks sensible.
/// </remarks>
[Trait("kind", "unit")]
public sealed class SystemPrincipalTests
{
    private static readonly OrganizationId Acme =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Beta =
        new(Guid.Parse("66666666-6666-4666-8666-666666666666"));

    /// <summary>
    /// IDN-PRIN-001 AC1: a system principal without a stated reason cannot be
    /// constructed, whichever scope it is of.
    /// </summary>
    /// <param name="reason">A reason that states nothing.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IDN_PRIN_001_AC1_APrincipalWithoutAReasonThrows(string reason)
    {
        Assert.Throws<ArgumentException>(() =>
            SystemPrincipal.ForOrganization("mailbox-reconciler", reason, Acme));

        Assert.Throws<ArgumentException>(() =>
            SystemPrincipal.ForDeployment("sweeper", reason, SystemOperation.ExpirySweep));
    }

    /// <summary>
    /// IDN-PRIN-001: the principal is named, so an audit record has an actor to carry.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_APrincipalWithoutANameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            SystemPrincipal.ForOrganization(" ", "nightly mailbox reconciliation", Acme));
    }

    /// <summary>
    /// IDN-PRIN-001 AC2: an organization-scoped principal acts for its own organization
    /// and for no other.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AC2_AnOrganizationScopedPrincipalReachesNoOther()
    {
        var principal = SystemPrincipal.ForOrganization(
            "mailbox-reconciler",
            "nightly mailbox reconciliation",
            Acme);

        Assert.True(principal.ActsFor(Acme));
        Assert.False(principal.ActsFor(Beta));
        Assert.False(principal.IsDeploymentScoped);
    }

    /// <summary>
    /// IDN-PRIN-001 AC3: a deployment-scoped principal runs the operations it names and
    /// no others.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AC3_ADeploymentScopedPrincipalRunsOnlyWhatItNames()
    {
        var principal = SystemPrincipal.ForDeployment(
            "retention-purge",
            "purging what has passed its declared retention",
            SystemOperation.RetentionPurge);

        Assert.True(principal.MayRun(SystemOperation.RetentionPurge));
        Assert.False(principal.MayRun(SystemOperation.Reconciliation));
        Assert.False(principal.MayRun(SystemOperation.RecordsOfProcessing));
        Assert.False(principal.MayRun(SystemOperation.ExpirySweep));
    }

    /// <summary>
    /// IDN-PRIN-001 AC3: a deployment-scoped principal serves no request, which is what
    /// acting for no organization means. It is pool-wide work and not work on anyone's
    /// behalf.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AC3_ADeploymentScopedPrincipalActsForNoOrganization()
    {
        var principal = SystemPrincipal.ForDeployment(
            "records-of-processing",
            "generating the records of processing",
            SystemOperation.RecordsOfProcessing);

        Assert.True(principal.IsDeploymentScoped);
        Assert.Null(principal.Organization);
        Assert.False(principal.ActsFor(Acme));
        Assert.False(principal.ActsFor(Beta));
    }

    /// <summary>
    /// IDN-PRIN-001: an organization-scoped principal runs none of the pool-wide
    /// operations, so the two scopes never overlap.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AnOrganizationScopedPrincipalRunsNoPoolWideOperation()
    {
        var principal = SystemPrincipal.ForOrganization(
            "mailbox-reconciler",
            "nightly mailbox reconciliation",
            Acme);

        Assert.Empty(principal.Operations);
        Assert.False(principal.MayRun(SystemOperation.Reconciliation));
    }

    /// <summary>
    /// IDN-PRIN-001 AC3: the restriction is an enumerated set, so a deployment-scoped
    /// principal that names none is not a principal.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AC3_ADeploymentScopedPrincipalNamingNoOperationThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            SystemPrincipal.ForDeployment("idle", "no reason to exist"));
    }

    /// <summary>
    /// IDN-PRIN-001 AC4: the reason is on the principal, so every action it takes has
    /// one to be audited with.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AC4_TheReasonIsCarriedOnThePrincipal()
    {
        var principal = SystemPrincipal.ForOrganization(
            "mailbox-reconciler",
            "nightly mailbox reconciliation",
            Acme);

        Assert.Equal("mailbox-reconciler", principal.Name);
        Assert.Equal("nightly mailbox reconciliation", principal.Reason);
    }
}
