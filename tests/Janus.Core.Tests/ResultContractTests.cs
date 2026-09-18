using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The shape of the outcome types and of the contracts that return them
/// (CONV-DESIGN-005, CONV-ERR-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class ResultContractTests
{
    private static readonly string[] ReachableOnAnOutcome =
    [
        "Equals",
        "GetHashCode",
        "Match",
        "Switch",
        "ToString",
    ];

    /// <summary>
    /// CONV-DESIGN-005 AC1: every method on a public contract returns an outcome, so
    /// no operation can report success by returning a bare value.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_005_AC1_EveryContractMethodReturnsAnOutcome()
    {
        foreach (MethodInfo method in ContractMethods())
        {
            Assert.True(
                IsOutcome(method.ReturnType),
                method.DeclaringType!.Name + "." + method.Name + " does not return an outcome.");
        }
    }

    /// <summary>
    /// CONV-DESIGN-005 AC2: no contract reports "not found" by returning null; it
    /// returns a failure carrying the named code.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_005_AC2_NoContractReturnsNullForNotFound()
    {
        var nullability = new NullabilityInfoContext();

        foreach (MethodInfo method in ContractMethods())
        {
            NullabilityInfo info = nullability.Create(method.ReturnParameter);

            Assert.True(
                Carried(info).All(carried => carried.ReadState != NullabilityState.Nullable),
                method.DeclaringType!.Name + "." + method.Name + " can return null.");
        }
    }

    /// <summary>
    /// CONV-ERR-001 AC2: the outcome is reachable only by handling both cases, so no
    /// accessor exists through which a caller could read a value and ignore a failure.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AC2_TheOutcomeIsReachableOnlyByHandlingBothCases()
    {
        foreach (Type outcome in new[] { typeof(Result), typeof(Result<>) })
        {
            Assert.Empty(outcome.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
            Assert.Empty(outcome.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static));
            Assert.Equal(
                ReachableOnAnOutcome,
                outcome
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(method => method.Name)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }
    }

    /// <summary>
    /// CONV-ERR-001 AC2, seen from the caller: both branches are required, so handling
    /// one case without the other does not compile and cannot be written here.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AC2_BothBranchesAreRequired()
    {
        foreach (Type outcome in new[] { typeof(Result), typeof(Result<>) })
        {
            IEnumerable<MethodInfo> branching = outcome
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method =>
                    string.Equals(method.Name, "Match", StringComparison.Ordinal)
                    || string.Equals(method.Name, "Switch", StringComparison.Ordinal));

            Assert.All(branching, method => Assert.Equal(2, method.GetParameters().Length));
        }
    }

    private static IEnumerable<MethodInfo> ContractMethods() =>
        typeof(Result).Assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface)
            .SelectMany(type => type.GetMethods())
            .Where(method => !method.IsSpecialName);

    private static IEnumerable<NullabilityInfo> Carried(NullabilityInfo info)
    {
        // A type parameter the contract leaves open carries whatever nullability the
        // caller's own type argument has, so it is not the contract returning null.
        if (info.Type.IsGenericParameter)
        {
            return [];
        }

        if (info.Type.IsGenericType)
        {
            return info.GenericTypeArguments.SelectMany(Carried);
        }

        return [info];
    }

    private static bool IsOutcome(Type type)
    {
        if (type == typeof(Result))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();

        if (definition == typeof(Result<>))
        {
            return true;
        }

        if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
        {
            return IsOutcome(type.GetGenericArguments()[0]);
        }

        return false;
    }
}
