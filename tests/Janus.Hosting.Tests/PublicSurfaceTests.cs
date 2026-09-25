using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the shipped assemblies may expose, derive from and carry: public types in the
/// contract's projects alone, one internal sealed implementation of each service
/// contract, one public registration, no controller, and no validation attribute
/// (LIB-API-002, CONV-LAYOUT-002, CONV-DESIGN-002, CONV-DESIGN-006, CONV-DESIGN-007,
/// CONV-CODE-006).
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

    // CONV-DESIGN-002 AC1: the interfaces of the core that are not service contracts,
    // since the host implements them and the library calls them: the extension points
    // of LIB-EXT-001, what the host declares of its model and its purposes
    // (LIB-HOST-001, LIB-HOST-002, LIB-HOST-004), and the receivers of what the library
    // publishes (LIB-API-001, IDN-LIFE-003a). An interface added outside this list is a
    // service contract and is held to the rule.
    private static readonly Type[] NotServiceContracts =
    [
        typeof(IAssuranceProvider),
        typeof(ICertificateRenewal),
        typeof(IClockReference),
        typeof(IDnsResolver),
        typeof(IErasureLedger),
        typeof(IEventConsumer<>),
        typeof(ILocationSource),
        typeof(IMailServer),
        typeof(IMailTransport),
        typeof(IMessageTemplates),
        typeof(INotificationHandler),
        typeof(IPurposeHandler),
        typeof(IRestoreTestInstance),
        typeof(ISecretSource),
        typeof(ISmsTransport),
        typeof(ISubjectEventSubscriber),
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

    /// <summary>
    /// CONV-DESIGN-002 AC1: every service contract of the core has exactly one
    /// implementation among the shipped assemblies, and it is internal and sealed. Every
    /// public interface of the core is a service contract but those the host implements
    /// for the library to call.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_002_AC1_EveryServiceContractHasOneInternalSealedImplementation()
    {
        Type[] implementations =
        [
            .. Shipped
                .SelectMany(name => Load(name).GetTypes())
                .Where(type => type is { IsClass: true, IsAbstract: false })
                .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)),
        ];

        Type[] contracts =
        [
            .. typeof(Result).Assembly
                .GetExportedTypes()
                .Where(type => type.IsInterface)
                .Where(type => !NotServiceContracts.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type)),
        ];

        IEnumerable<string> broken = contracts
            .Select(contract => (contract, Implementing: implementations.Where(contract.IsAssignableFrom).ToArray()))
            .Where(found => found.Implementing.Length != 1
                || found.Implementing[0] is not { IsSealed: true, IsPublic: false, IsNestedPublic: false })
            .Select(found => found.contract.Name + ": " + string.Join(", ", found.Implementing.Select(type => type.FullName)));

        Assert.NotEmpty(contracts);
        Assert.Empty(broken);
    }

    /// <summary>
    /// CONV-DESIGN-007 AC1: a host registers the library with one call, since the only
    /// public extension of the service collection in the shipped assemblies is
    /// <c>AddJanus</c>, and it is declared once.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC1_TheEntryPointIsThePublicRegistrationAndTheOnlyOne()
    {
        IEnumerable<string> registrations = Shipped
            .SelectMany(name => Load(name).GetExportedTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(ExtensionAttribute), inherit: false))
            .Where(method => typeof(IServiceCollection).IsAssignableFrom(method.GetParameters()[0].ParameterType))
            .Select(method => method.DeclaringType!.FullName + "." + method.Name);

        Assert.Equal([typeof(HostingRegistration).FullName + "." + nameof(HostingRegistration.AddJanus)], registrations);
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
