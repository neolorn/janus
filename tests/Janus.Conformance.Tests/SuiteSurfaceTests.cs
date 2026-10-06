using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Janus.Core;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// What the suite a host runs, and the contract it asks for the provider probes
/// through, may carry: no client secret, in either direction (LIB-TEST-001 AC5, D-172).
/// </summary>
[Trait("kind", "contract")]
public sealed class SuiteSurfaceTests
{
    // Every member a type exposes, declared on it or inherited.
    private const BindingFlags Exposed = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    // How a secret is held and handed over in the library: as bytes (CONV-CODE-007).
    private static readonly Type[] Bytes =
    [
        typeof(byte[]),
        typeof(Memory<byte>),
        typeof(ReadOnlyMemory<byte>),
        typeof(Span<byte>),
        typeof(ReadOnlySpan<byte>),
    ];

    /// <summary>
    /// LIB-TEST-001 AC5: no public type of the suite, and no member of the contract in the
    /// core through which it asks for the provider probes, carries or receives a secret:
    /// nothing exposed is named for one or holds the bytes one is held in.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC5_NeitherTheSuiteNorTheProbeContractCarriesASecret()
    {
        Type[] surface = [.. typeof(ConformanceSuite).Assembly.GetExportedTypes(), typeof(IProviderProbes)];

        string[] carried = [.. surface.SelectMany(Carrying)];

        Assert.Contains(surface, type => type == typeof(ConformanceSuite));
        Assert.Contains(
            nameof(ConformanceSuite.ProviderAsync),
            typeof(ConformanceSuite).GetMethods(Exposed).Select(method => method.Name));
        Assert.Empty(carried);
    }

    /// <summary>
    /// LIB-TEST-001 AC5: the reading of a surface finds a secret where one is carried,
    /// as the suite's own client once carried it.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC5_ASurfaceCarryingASecretIsFound()
    {
        string[] carried = [.. Carrying(typeof(ICarrier))];

        Assert.Contains(carried, found => found.EndsWith(".Secret", StringComparison.Ordinal));
        Assert.Contains(carried, found => found.EndsWith(".Present(clientSecret)", StringComparison.Ordinal));
    }

    // Each exposed member, parameter or answer of a type that is named for a secret or
    // holds the bytes one is held in.
    private static IEnumerable<string> Carrying(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(Exposed))
        {
            if (Named(property.Name) || Held(property.PropertyType))
            {
                yield return type.FullName + "." + property.Name;
            }
        }

        foreach (FieldInfo field in type.GetFields(Exposed))
        {
            if (Named(field.Name) || Held(field.FieldType))
            {
                yield return type.FullName + "." + field.Name;
            }
        }

        foreach (MethodBase method in type.GetMethods(Exposed).Cast<MethodBase>().Concat(type.GetConstructors()))
        {
            if (method is MethodInfo answering && Held(answering.ReturnType))
            {
                yield return type.FullName + "." + method.Name;
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (Named(parameter.Name ?? string.Empty) || Held(parameter.ParameterType))
                {
                    yield return type.FullName + "." + method.Name + "(" + parameter.Name + ")";
                }
            }
        }
    }

    private static bool Named(string name) => name.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static bool Held(Type type) =>
        Bytes.Contains(type)
        || (type.HasElementType && Held(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(Held));

    // A surface as the suite's client once was: a secret handed out and handed in.
    private interface ICarrier
    {
        ReadOnlyMemory<byte> Secret { get; }

        void Present(string clientSecret);
    }
}
