using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Callbacks;
using Xunit;

namespace Janus.Hosting.Tests.Callbacks;

/// <summary>
/// The contracts a host implements for its callbacks are held to the same rule as every
/// other contract: an expected failure is an outcome, and nothing is null
/// (CONV-DESIGN-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class CallbackContractTests
{
    private static readonly Type[] Contracts = [typeof(ISignedCallback), typeof(IUnsignedCallback)];

    /// <summary>
    /// CONV-DESIGN-005 AC1: every method of a callback contract returns an outcome, so a
    /// host's scheme reports what it could not read as a failure the profile refuses.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_005_AC1_EveryCallbackContractMethodReturnsAnOutcome()
    {
        foreach (MethodInfo method in Methods())
        {
            Assert.True(
                IsOutcome(method.ReturnType),
                method.DeclaringType!.Name + "." + method.Name + " does not return an outcome.");
        }
    }

    /// <summary>
    /// CONV-DESIGN-005 AC2: no callback contract answers "not there" with null.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_005_AC2_NoCallbackContractReturnsNull()
    {
        var nullability = new NullabilityInfoContext();

        foreach (MethodInfo method in Methods())
        {
            Assert.DoesNotContain(
                Carried(nullability.Create(method.ReturnParameter)),
                carried => carried.ReadState is NullabilityState.Nullable);
        }
    }

    private static IEnumerable<MethodInfo> Methods() =>
        Contracts.SelectMany(contract => contract.GetMethods()).Where(method => !method.IsSpecialName);

    private static IEnumerable<NullabilityInfo> Carried(NullabilityInfo info) =>
        [info, .. info.GenericTypeArguments.SelectMany(Carried)];

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

        return definition == typeof(Result<>)
            || (definition == typeof(ValueTask<>) && IsOutcome(type.GetGenericArguments()[0]));
    }
}
