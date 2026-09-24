using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Privacy.Tests;

/// <summary>
/// What a deployment has to have configured before it starts: a retention period for
/// every data category it declares a purpose over, and a written-consent basis where
/// it is open to minors (PRIV-RET-001, PRIV-MINOR-001, LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ConfigurationCoverageTests
{
    private readonly ConfigurationInMemory _configuration = new();

    /// <summary>
    /// PRIV-RET-001 AC1: the deployment declares purposes over two categories and
    /// names a period for one, so it does not start, and the failure names the key
    /// nobody set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC1_ADeclaredCategoryWithNoPeriodFailsStartupAsync()
    {
        _configuration.Set(Settings.HostCategoryRetention, "identity", TimeSpan.FromDays(365));

        Result outcome = await ValidatedAsync();

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));

        Assert.Equal(
            "retention.statement",
            outcome.Match(() => null, failure => failure.Details["key"].GetString()));
    }

    /// <summary>
    /// PRIV-RET-001 AC1: every declared category with a period is what the check is
    /// looking for, and the deployment starts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC1_EveryDeclaredCategoryWithAPeriodStartsAsync()
    {
        Kept();

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
        Kept();
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        AuthorizationDeclaration adults = new AuthorizationDeclarationBuilder()
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
        Kept();
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        Assert.True((await ValidatedAsync()).Match(() => true, _ => false));

        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        AuthorizationDeclaration adults = new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .Permission("statement:read")
            .Resource<Declaration.Statement>("statement", statement => statement
                .BelongsToOrganization()
                .Purpose("performance", "contract", data: ["identity", "statement"], subjects: ["customers"]))
            .Build();

        Assert.True((await ValidatedAsync(adults)).Match(() => true, _ => false));
    }

    private void Kept()
    {
        _configuration.Set(Settings.HostCategoryRetention, "identity", TimeSpan.FromDays(365));
        _configuration.Set(Settings.HostCategoryRetention, "statement", TimeSpan.FromDays(1826));
    }

    private ValueTask<Result> ValidatedAsync() => ValidatedAsync(Declaration.Authorization);

    private ValueTask<Result> ValidatedAsync(AuthorizationDeclaration declared) =>
        new ConfigurationCoverage(
            declared,
            DeclaredProcessing.Of(declared),
            _configuration).ValidateAsync(TestContext.Current.CancellationToken);
}
