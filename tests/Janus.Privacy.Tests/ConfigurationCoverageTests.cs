using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Privacy.Tests;

/// <summary>
/// What a deployment has to have configured before it starts: a retention period no
/// shorter than its floor for every data category it declares a purpose over, and a
/// written-consent basis where it is open to minors (PRIV-RET-001, PRIV-MINOR-001,
/// LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConfigurationCoverageTests
{
    private readonly ConfigurationInMemory _configuration = new();

    /// <summary>
    /// PRIV-RET-001 AC1: the deployment declares purposes over two categories and a
    /// floor for one, so it does not start, and the failure names the key that has no
    /// period.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC1_ADeclaredCategoryWithNoFloorFailsStartupAsync()
    {
        AuthorizationDeclaration declared = Declaration.Authorization with
        {
            RetentionFloors = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
            {
                ["identity"] = TimeSpan.FromDays(365),
            },
        };

        Result outcome = await ValidatedAsync(declared);

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));

        Assert.Equal(
            "retention.statement",
            outcome.Match(() => null, failure => failure.Details["key"].GetString()));
    }

    /// <summary>
    /// PRIV-RET-001 AC1: a deployment that states no period for any category keeps
    /// each for its declared floor, and starts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC1_EveryDeclaredCategoryStartsOnItsFloorAsync() =>
        Assert.True((await ValidatedAsync()).Match(() => true, _ => false));

    /// <summary>
    /// PRIV-RET-001 AC2: a period stated below the category's floor stops the
    /// deployment, naming the key and the floor it missed; the floor itself, and a
    /// period above it, start.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC2_APeriodStatedBelowTheFloorFailsStartupAsync()
    {
        _configuration.Set(Settings.HostCategoryRetention, "statement", TimeSpan.FromDays(1825));

        Result outcome = await ValidatedAsync();

        Assert.Equal(
            ErrorCodes.ConfigurationValueBelowFloor,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));

        Assert.Equal(
            ("retention.statement", "P1826D"),
            outcome.Match(
                () => (null, null),
                failure => (failure.Details["key"].GetString(), failure.Details["floor"].GetString())));

        _configuration.Set(Settings.HostCategoryRetention, "statement", TimeSpan.FromDays(1826));

        Assert.True((await ValidatedAsync()).Match(() => true, _ => false));

        _configuration.Set(Settings.HostCategoryRetention, "statement", TimeSpan.FromDays(3652));

        Assert.True((await ValidatedAsync()).Match(() => true, _ => false));
    }

    /// <summary>
    /// PRIV-MINOR-001 AC4: a deployment that takes minors and declares no
    /// written-consent basis does not start, and the failure names what is missing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC4_ADeploymentOpenToMinorsWithNoWrittenConsentBasisFailsStartupAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        AuthorizationDeclaration adults = new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .RetentionFloor("statement", TimeSpan.FromDays(365))
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .Permission("statement:read")
            .Resource<Declaration.Statement>("statement", statement => statement
                .BelongsToOrganization()
                .Sensitive("financial")
                .Purpose("performance", "contract", data: ["identity", "statement"], subjects: ["customers"]))
            .Build();

        Result outcome = await ValidatedAsync(adults);

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));

        Assert.Equal(
            "lawfulbasis.writtenconsent",
            outcome.Match(() => null, failure => failure.Details["key"].GetString()));
    }

    /// <summary>
    /// PRIV-MINOR-001 AC4: the same deployment with a written-consent basis declared
    /// starts, and so does one that is 18+ only without it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC4_AWrittenConsentBasisOrAnAdultOnlyServiceStartsAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        Assert.True((await ValidatedAsync()).Match(() => true, _ => false));

        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        AuthorizationDeclaration adults = new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .RetentionFloor("statement", TimeSpan.FromDays(365))
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .Permission("statement:read")
            .Resource<Declaration.Statement>("statement", statement => statement
                .BelongsToOrganization()
                .Purpose("performance", "contract", data: ["identity", "statement"], subjects: ["customers"]))
            .Build();

        Assert.True((await ValidatedAsync(adults)).Match(() => true, _ => false));
    }

    private ValueTask<Result> ValidatedAsync() => ValidatedAsync(Declaration.Authorization);

    private ValueTask<Result> ValidatedAsync(AuthorizationDeclaration declared) =>
        new ConfigurationCoverage(
            declared,
            DeclaredProcessing.Of(declared),
            new CategoryRetention(declared, _configuration),
            _configuration).ValidateAsync(TestContext.Current.CancellationToken);
}
