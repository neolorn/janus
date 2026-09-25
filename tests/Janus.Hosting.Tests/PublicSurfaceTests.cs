using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the shipped assemblies may expose, derive from and carry: public types in the
/// contract's projects alone, no controller, and no validation attribute
/// (LIB-API-002, CONV-LAYOUT-002, CONV-DESIGN-006, CONV-CODE-006).
/// </summary>
[Trait("kind", "contract")]
public sealed class PublicSurfaceTests
{
    // Every member a type declares, whatever its visibility.
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    // CONV-LAYOUT-002: the projects whose every type is internal.
    private static readonly string[] Internal =
    [
        "Janus.Authentication",
        "Janus.Authorization",
        "Janus.Cli",
        "Janus.Identity",
        "Janus.Privacy",
        "Janus.Storage",
    ];

    // CONV-LAYOUT-001: every project the two packages ship. The analysers run inside
    // the compiler, not in a host.
    private static readonly string[] Shipped =
    [
        .. Internal,
        "Janus.Conformance",
        "Janus.Core",
        "Janus.Hosting",
    ];

    /// <summary>
    /// LIB-API-002 AC1: no type of the areas, the storage or the command line can be
    /// reached from outside its assembly, and the hosting project exposes its mounting
    /// types, the model-builder extension and the registration entry point and nothing
    /// else (CONV-LAYOUT-002).
    /// </summary>
    [Fact]
    public void LIB_API_002_AC1_InternalTypesAreNotPubliclyAccessible()
    {
        Type[] mounting =
        [
            typeof(AuthorizationTables),
            typeof(ApplicationKind),
            typeof(PipelineProfiles),
            typeof(CallbackDelivery),
            typeof(CallbackSecrets),
            typeof(CallbackSignature),
            typeof(ISignedCallback),
            typeof(IUnsignedCallback),
            typeof(HostingRegistration),
            typeof(IdentityEndpoints),
        ];

        IEnumerable<Type> exposed = Internal.SelectMany(name => Load(name).GetExportedTypes());

        Assert.Empty(exposed);
        Assert.Equal(
            mounting.Select(type => type.FullName).Order(StringComparer.Ordinal),
            typeof(HostingRegistration).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// CONV-DESIGN-006 AC1: no type of any shipped assembly is a controller, so every
    /// endpoint is a minimal API mapping.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC1_NoTypeDerivesFromControllerBase()
    {
        IEnumerable<Type> controllers = Shipped
            .SelectMany(name => Load(name).GetTypes())
            .Where(typeof(ControllerBase).IsAssignableFrom);

        Assert.Empty(controllers);
    }

    /// <summary>
    /// CONV-CODE-006 AC1: no type of any shipped assembly, no member it declares and no
    /// parameter of one carries a validation attribute, so shape is checked by a guard
    /// at the boundary and a domain type is valid by construction.
    /// </summary>
    [Fact]
    public void CONV_CODE_006_AC1_NoValidationAttributeAppearsOnAType()
    {
        IEnumerable<string> validated = Shipped
            .SelectMany(name => Load(name).GetTypes())
            .SelectMany(Carriers)
            .Where(carrier => carrier.Attributes.Any(Validates))
            .Select(carrier => carrier.Name);

        Assert.Empty(validated);
    }

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

    // A type and everything declared on it that can carry an attribute.
    private static IEnumerable<(string Name, IEnumerable<CustomAttributeData> Attributes)> Carriers(Type type) =>
        type
            .GetMembers(Declared)
            .Select(member => (type.FullName + "." + member.Name, member.CustomAttributes))
            .Concat(type
                .GetMethods(Declared)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(Declared))
                .SelectMany(method => method.GetParameters())
                .Select(parameter => (type.FullName + "." + parameter.Member.Name + "(" + parameter.Name + ")", parameter.CustomAttributes)))
            .Prepend((type.FullName!, type.CustomAttributes));

    // An attribute of the validation namespace, or one derived from its base wherever
    // it is declared.
    private static bool Validates(CustomAttributeData attribute) =>
        typeof(ValidationAttribute).IsAssignableFrom(attribute.AttributeType)
            || string.Equals(attribute.AttributeType.Namespace, typeof(ValidationAttribute).Namespace, StringComparison.Ordinal);
}
