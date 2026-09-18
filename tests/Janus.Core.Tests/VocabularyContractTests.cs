using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The names the vocabularies of chapter 10 section 5 carry on the wire. They are part
/// of the stable contract: a host reads them from a policy and an endpoint declares
/// them, so a rename is a breaking change and fails here (LIB-API-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class VocabularyContractTests
{
    // Chapter 10 section 5a, in the order the table gives them.
    private static readonly string[] StepUpActions =
    [
        "password:set",
        "identifier:add",
        "identifier:remove",
        "username:change",
        "factor:enrol",
        "factor:remove",
        "recoverycodes:generate",
        "mailcredential:create",
        "mailcredential:revoke",
        "privacy:export",
        "account:delete",
        "account:deactivate",
        "provider:link",
        "provider:unlink",
        "recovery:approve",
        "invitation:issue",
        "grant:manage",
        "account:suspend",
        "account:reactivate",
        "account:takedown",
        "account:takedownreverse",
        "erasure:complete",
        "config:loosen",
        "alerting:destinations",
        "policy:change",
        "domain:manage",
        "restriction:edit",
        "restriction:grant",
        "breakglass:replace",
    ];

    // The catalogue of chapter 02 AUTH-FACT-002 that carries an identifier.
    private static readonly string[] Factors =
    [
        "password",
        "passkey",
        "emailLink",
        "emailCode",
        "phoneLink",
        "google",
        "apple",
        "totp",
        "securityKey",
        "phoneCode",
        "recoveryCodes",
        "breakGlass",
    ];

    /// <summary>
    /// LIB-API-001 AC2: the step-up action names are the keys of a policy's gates, so
    /// the set is fixed and a rename is caught here.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheStepUpActionNamesAreTheContract() =>
        Assert.Equal(StepUpActions.Order(StringComparer.Ordinal), WireNames<StepUpAction>());

    /// <summary>
    /// LIB-API-001 AC2: the factor identifiers are the members of a policy's login
    /// factors and the values an endpoint declares.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheFactorIdentifiersAreTheContract() =>
        Assert.Equal(Factors.Order(StringComparer.Ordinal), WireNames<Factor>());

    /// <summary>
    /// LIB-API-001 AC2: the assurance levels of chapter 10 section 5.4, the gate levels
    /// of section 4.1a and the redundancy values it names.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_ThePolicyFieldValuesAreTheContract()
    {
        Assert.Equal(["aal1", "aal2", "aal3", "delegated"], WireNames<AssuranceLevel>());
        Assert.Equal(["aal1", "aal2", "reachable"], WireNames<GateLevel>());
        Assert.Equal(["advisory", "enforced"], WireNames<CredentialRedundancy>());
    }

    /// <summary>
    /// Every member of every vocabulary carries a wire name, so none of them reaches a
    /// host as the name the compiler happens to give it.
    /// </summary>
    [Fact]
    public void WireNames_EveryVocabularyMember_CarriesOne()
    {
        Type[] vocabularies =
        [
            typeof(StepUpAction),
            typeof(Factor),
            typeof(AssuranceLevel),
            typeof(GateLevel),
            typeof(CredentialRedundancy),
        ];

        foreach (Type vocabulary in vocabularies)
        {
            foreach (string member in Enum.GetNames(vocabulary))
            {
                Assert.NotNull(
                    vocabulary.GetField(member)!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>());
            }
        }
    }

    private static string[] WireNames<TVocabulary>()
        where TVocabulary : struct, Enum =>
        [.. Enum.GetNames<TVocabulary>()
            .Select(member => typeof(TVocabulary)
                .GetField(member)!
                .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!
                .Name)
            .Order(StringComparer.Ordinal)];
}
