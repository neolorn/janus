using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Janus.Hosting.Tests.Authorization;
using Janus.Hosting.Tests.Bff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the shipped assemblies may expose, derive from and carry: public types in the
/// contract's projects alone, one internal sealed implementation of each service
/// contract whose operations meet the gate first, one public registration, no
/// controller, no validation attribute, and no member that takes a typed value as the
/// type it is stored in (LIB-API-002, CONV-LAYOUT-002, CONV-DESIGN-002, CONV-DESIGN-004,
/// CONV-DESIGN-006, CONV-DESIGN-007, CONV-CODE-006).
/// </summary>
[Trait("kind", "contract")]
public sealed class PublicSurfaceTests
{
    // A connection nothing is opened on.
    private const string Connection = "Host=nowhere;Database=identity";

    // CONV-DESIGN-007 AC8: the project whose own the shared framework's types are.
    private const string Mounting = "Janus.Hosting";

    // CONV-DESIGN-007: the shared framework a project takes the container's abstractions
    // from.
    private const string SharedFramework = "Microsoft.AspNetCore.App";

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
    // since the host implements them and the library calls them, each beside the item
    // that makes the host implement it. An interface added outside this list is a
    // service contract and is held to the rule.
    private static readonly Type[] NotServiceContracts =
    [
        typeof(IAssuranceProvider), // LIB-HOST-004: assurance where authentication is not the library's.
        typeof(ICertificateRenewal), // LIB-HOST-001, optional: an environment seam (INF-TLS-003).
        typeof(IClockReference), // LIB-HOST-001, optional: an environment seam (INF-HOST-001).
        typeof(IDatacenterRangeSource), // LIB-HOST-001, optional: an environment seam (AUTH-ABUSE-008).
        typeof(IDnsResolver), // LIB-HOST-001, optional: an environment seam (REG-DOM-001).
        typeof(IErasureLedger), // LIB-HOST-001, optional: an environment seam (DR-016).
        typeof(IEventConsumer<>), // LIB-API-001 and IDN-LIFE-003a: the receiver of an emitted event.
        typeof(ILocationSource), // LIB-HOST-001, optional: an environment seam (INT-GEN-006).
        typeof(IMailServer), // LIB-HOST-001, optional: an environment seam (INT-MAIL-001).
        typeof(IMailTransport), // LIB-EXT-001 and LIB-HOST-001: mail delivery.
        typeof(IMessageTemplates), // LIB-EXT-001: message and content templates.
        typeof(INotificationHandler), // LIB-EXT-001: notification handling.
        typeof(IPurposeHandler), // LIB-HOST-001: a purpose handler.
        typeof(IRestoreTestInstance), // LIB-HOST-001, optional: an environment seam (DR-007).
        typeof(ISecretSource), // LIB-EXT-001 and LIB-HOST-001: the secret source.
        typeof(ISmsTransport), // LIB-EXT-001 and LIB-HOST-001: SMS delivery.
        typeof(ISubjectEventSubscriber), // LIB-HOST-001: a subject-event handler.
    ];

    // CONV-DESIGN-007 AC7: the registration method of each project, other than the
    // hosting project, that defines a type the container registers. A project left out
    // defines none and exposes none.
    private static readonly Dictionary<string, string> RegistrationMethods = new(StringComparer.Ordinal)
    {
        ["Janus.Authentication"] = "AddAuthenticationArea",
        ["Janus.Authorization"] = "AddAuthorizationArea",
        ["Janus.Core"] = "AddCoreArea",
        ["Janus.Privacy"] = "AddPrivacyArea",
        ["Janus.Storage"] = "AddStorageArea",
    };

    // CONV-DESIGN-007 AC8: a namespace of the shared framework's own, wherever a file
    // names it.
    private static readonly Regex FrameworkNamespace = new(
        @"\bMicrosoft\s*\.\s*AspNetCore\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-007 AC8: a namespace of the framework's extensions, which the same
    // reference brings, with the part of them the file names.
    private static readonly Regex ExtensionsNamespace = new(
        @"\bMicrosoft\s*\.\s*Extensions\s*\.\s*(?<part>\w+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-004 AC2: a parameter of the underlying type where the library has a
    // type of its own for the thing.
    private static readonly Regex Untyped = new(
        @"[(,]\s*(?:(?<wrapped>Guid)\s+[a-z]|(?<wrapped>string)\s+(?:subject|organization|email|phone|username|address)\b)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-004 AC2: the type a typed identifier or value wraps, read from the
    // value its declaration holds.
    private static readonly Regex Wrapped = new(
        @"\b(?<wrapped>Guid|string)\s+Value\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // CONV-DESIGN-004 AC2: the members whose parameters an interface of a package
    // fixes, each as the type that declares it and the member. The provider's stores
    // name the OIDC subject as text, and the document retrievers take an address as
    // text. No other member of these types is exempt.
    private static readonly (string Type, string Member)[] ForeignMembers =
    [
        ("OidcAuthorizationStore", "FindBySubjectAsync"),
        ("OidcAuthorizationStore", "RevokeBySubjectAsync"),
        ("OidcTokenStore", "FindBySubjectAsync"),
        ("OidcTokenStore", "RevokeBySubjectAsync"),
        ("ProviderDocuments", "GetDocumentAsync"),
        ("ProviderMetadataReading", "GetConfigurationAsync"),
    ];

    // CONV-DESIGN-004 AC2: the folders the repository's projects are written under.
    private static readonly string[] ProjectRoots = ["src", "tools"];

    // CONV-DESIGN-008: the packages the table names only as ones a listed package
    // brings, which no project references directly.
    private static readonly string[] BroughtPackages =
    [
        "Microsoft.IdentityModel.JsonWebTokens",
        "Microsoft.IdentityModel.Protocols",
    ];

    // Every type the shipped assemblies declare.
    private static readonly Type[] ShippedTypes = [.. Shipped.SelectMany(name => Load(name).GetTypes())];

    // CONV-DESIGN-002 AC3: the collaborators a call on which is the gate step: the gate,
    // the check asked in the administrative organization, the restriction on an
    // account's changes to its own settings, and the refusal of what names no
    // organization.
    private static readonly string[] GateSteps =
    [
        "IAccessGate",
        "AdministrativeScope",
        "ISettingsRestriction",
        "IUnscopedRefusal",
    ];

    // CONV-DESIGN-002 AC3, AUTHZ-SCOPE-001: the call that reads which organization a row
    // belongs to and nothing else of it. It resolves where the gate is asked, so it may
    // come before the gate, and it answers an organization or nothing.
    private const string ScopeResolution = "ScopeOfAsync";

    // CONV-DESIGN-002 AC3: the operations on the caller's own account, credentials,
    // sessions, consent, requests and invitations. No permission governs them
    // (AUTHZ-GATE-006, IDN-ACCT-007), so their gate step is asking whose account is
    // asking, and it is asked before any call is made.
    private static readonly string[] OwnOperations =
    [
        nameof(IAccount) + "." + nameof(IAccount.DeactivateAsync),
        nameof(IAccount) + "." + nameof(IAccount.DeleteAsync),
        nameof(IAccount) + "." + nameof(IAccount.ListCredentialsAsync),
        nameof(IAccount) + "." + nameof(IAccount.ReadAsync),
        nameof(IAccount) + "." + nameof(IAccount.ReadPhotoAsync),
        nameof(IAccount) + "." + nameof(IAccount.ReadPreferencesAsync),
        nameof(IAppPasswords) + "." + nameof(IAppPasswords.CreateAsync),
        nameof(IAppPasswords) + "." + nameof(IAppPasswords.ListAsync),
        nameof(IAppPasswords) + "." + nameof(IAppPasswords.RevokeAsync),
        nameof(IAuthentication) + "." + nameof(IAuthentication.ForgetDeviceAsync),
        nameof(IAuthentication) + "." + nameof(IAuthentication.ListDevicesAsync),
        nameof(IAuthentication) + "." + nameof(IAuthentication.StepUpAsync),
        nameof(ICredentials) + "." + nameof(ICredentials.MarkRecoveryCodesExportedAsync),
        nameof(IConsents) + "." + nameof(IConsents.GrantAsync),
        nameof(IConsents) + "." + nameof(IConsents.ObjectAsync),
        nameof(IConsents) + "." + nameof(IConsents.ObjectionsAsync),
        nameof(IConsents) + "." + nameof(IConsents.ReadAsync),
        nameof(IConsents) + "." + nameof(IConsents.WithdrawAsync),
        nameof(IConsents) + "." + nameof(IConsents.WithdrawObjectionAsync),
        nameof(IExports) + "." + nameof(IExports.AssembleAsync),
        nameof(IInvitations) + "." + nameof(IInvitations.AcknowledgeAsync),
        nameof(IInvitations) + "." + nameof(IInvitations.AttachedAsync),
        nameof(IOidc) + "." + nameof(IOidc.ClaimsAsync),
        nameof(IPrivacyRequests) + "." + nameof(IPrivacyRequests.SubmitAsync),
        nameof(IRecovery) + "." + nameof(IRecovery.ReportLossAsync),
        nameof(ISessions) + "." + nameof(ISessions.EndAsync),
        nameof(ISessions) + "." + nameof(ISessions.EndEverywhereAsync),
        nameof(ISessions) + "." + nameof(ISessions.ListAsync),
        nameof(ISessions) + "." + nameof(ISessions.ReadAsync),
    ];

    // CONV-DESIGN-002 AC3: the operations no gate governs. Counting the records one
    // person was given watches that person, and a refusal would silence the watch
    // (OPS-ALERT-005). A refresh writes what the host's rows already say, for the host's
    // own operation that met the gate (AUTHZ-DERIVE-005). A loss report is cancelled on
    // the token its notification carried or by its holder, judged against the one
    // report, and every other case is refused alike (AUTH-RECOV-007). A registration is
    // begun by a browser that holds no account yet, and one already signed in is refused
    // on its context before anything else (REG-SESS-002). The provider probes are asked
    // by the host running the conformance suite and read no record of a person
    // (LIB-TEST-001, D-172). The gate itself is left out, since it is the gate.
    private static readonly string[] Ungated =
    [
        nameof(IDerivationMaterialiser) + "." + nameof(IDerivationMaterialiser.RefreshAsync),
        nameof(IProviderProbes) + "." + nameof(IProviderProbes.RunAsync),
        nameof(IReadVolume) + "." + nameof(IReadVolume.ReturnedAsync),
        nameof(IRecovery) + "." + nameof(IRecovery.CancelLossAsync),
        nameof(IRegistration) + "." + nameof(IRegistration.BeginAsync),
    ];

    // A comment to the end of its line, which names what it likes and calls nothing.
    private static readonly Regex Comment = new(
        @"//.*",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // A call to an asynchronous method, on a collaborator or on the type itself.
    private static readonly Regex Call = new(
        @"(?:\b(?<target>\w+)\s*\??\s*\.\s*)?\b(?<name>\w+Async)\s*(?:<[^<>()]*>)?\s*\(",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // Asking whose account is asking.
    private static readonly Regex Identity = new(
        @"\bcontext\s*\??\s*\.\s*(?:Effective|Acting)\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // The caller, handed on.
    private static readonly Regex Caller = new(
        @"\bcontext\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // One parameter of a declaration: its type, bare of arguments, and its name.
    private static readonly Regex Parameter = new(
        @"(?<type>[\w.]+)(?:<.*>)?\??\s+(?<name>\w+)\s*(?:=.*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(5));

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
    /// CONV-DESIGN-004 AC2: no member of any project takes a typed identifier or value
    /// as the type it is stored in. A parameter of the type a typed identifier or value
    /// wraps, inside its own declaration, is the value it is made from, and a member an
    /// interface of a package fixes takes what that interface declares; every other
    /// match is reported with its type and member.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC2_NoMethodTakesAValueAsItsUnderlyingType()
    {
        IEnumerable<string> taking = EveryProject()
            .SelectMany(Sources)
            .Select(file => (Type: Path.GetFileNameWithoutExtension(file), Text: File.ReadAllText(file)))
            .SelectMany(one => Untyped
                .Matches(one.Text)
                .Where(match => !MadeFrom(one.Type, one.Text, match.Groups["wrapped"].Value))
                .Select(match => (one.Type, Member: Taking(one.Text, match.Index)))
                .Where(member => !ForeignMembers.Contains(member))
                .Select(member => member.Type + "." + member.Member));

        Assert.Empty(taking);
    }

    /// <summary>
    /// CONV-DESIGN-004 AC2: a member is exempt only where a package's interface fixes
    /// its parameters: each exempt member is the target of an interface map entry whose
    /// interface is declared in an assembly of a package CONV-DESIGN-008 lists or names
    /// as one a listed package brings, the provider's abstractions among them, which
    /// the listed provider packages bring (D-166, D-183).
    /// </summary>
    [Fact]
    public void CONV_DESIGN_004_AC2_OnlyAMemberAPackagesInterfaceFixesIsExempt()
    {
        string[] packages =
        [
            .. XDocument
                .Load(Path.Combine(Repository.Root(), "Directory.Packages.props"))
                .Descendants("PackageVersion")
                .Select(package => package.Attribute("Include")!.Value),
            .. BroughtPackages,
        ];

        IEnumerable<string> unfixed = ForeignMembers
            .Where(member => !ShippedTypes
                .Where(type => type.Name == member.Type)
                .SelectMany(type => type.GetInterfaces().Select(type.GetInterfaceMap))
                .Where(map => OfAPackage(map.InterfaceType.Assembly.GetName().Name!, packages))
                .SelectMany(map => map.TargetMethods)
                .Any(target => target.Name == member.Member
                    || target.Name.EndsWith("." + member.Member, StringComparison.Ordinal)))
            .Select(member => member.Type + "." + member.Member);

        Assert.Empty(unfixed);
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
        Type[] contracts = ServiceContracts();

        IEnumerable<string> broken = contracts
            .Select(contract => (contract, Implementing: Implementations().Where(contract.IsAssignableFrom).ToArray()))
            .Where(found => found.Implementing.Length != 1
                || found.Implementing[0] is not { IsSealed: true, IsPublic: false, IsNestedPublic: false })
            .Select(found => found.contract.Name + ": " + string.Join(", ", found.Implementing.Select(type => type.FullName)));

        Assert.NotEmpty(contracts);
        Assert.Empty(broken);
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3: every operation of a service contract meets the gate before
    /// it reads or writes. Its first asynchronous call, followed into the method of its
    /// own or of a collaborator it hands the caller to, is a call on the gate. Reading
    /// only which organization a row belongs to resolves where the gate is asked and may
    /// come first (AUTHZ-SCOPE-001). An operation on the caller's own records asks whose
    /// account is asking before it makes any call, and the operations no gate governs
    /// are named with their reasons.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_002_AC3_EveryOperationMeetsTheGateBeforeItReadsOrWrites()
    {
        (string Name, MethodInfo Implementing)[] operations =
        [
            .. ServiceContracts()
                .Where(contract => contract != typeof(IAccessGate))
                .SelectMany(Operations),
        ];

        IEnumerable<string> unmet = operations
            .Where(operation => !Ungated.Contains(operation.Name))
            .Select(operation => (operation.Name, Unmet: BeforeTheGate(
                operation.Implementing.DeclaringType!,
                operation.Implementing.Name,
                [.. operation.Implementing.GetParameters().Select(parameter => parameter.Name!)],
                OwnOperations.Contains(operation.Name),
                depth: 0)))
            .Where(found => found.Unmet is not null)
            .Select(found => found.Name + ": " + found.Unmet);

        IEnumerable<string> resolving = ShippedTypes
            .SelectMany(type => type.GetMethods(Declared))
            .Where(method => method.Name == ScopeResolution)
            .Where(method => method.ReturnType != typeof(ValueTask<OrganizationId?>))
            .Select(method => method.DeclaringType!.FullName + "." + method.Name);

        Assert.NotEmpty(operations);
        Assert.Empty(OwnOperations.Concat(Ungated).Except(operations.Select(operation => operation.Name)));
        Assert.Empty(resolving);
        Assert.Empty(unmet);
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3, LIB-API-005: the read of the provider's published key set
    /// is no operation. It takes no access context, so no gate is asked of it, and what
    /// it answers is the public key, its identifier, its algorithm and when it retires,
    /// and nothing of a person or of a private key.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_002_AC3_TheReadOfThePublishedKeySetTakesNoAccessContextAndAnswersPublicKeysAlone()
    {
        MethodInfo read = typeof(IOidc).GetMethod(nameof(IOidc.KeysAsync))!;

        Assert.Equal([typeof(CancellationToken)], read.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(ValueTask<Result<IReadOnlyList<PublishedSigningKey>>>), read.ReturnType);
        Assert.Equal(
            [
                nameof(PublishedSigningKey.Algorithm),
                nameof(PublishedSigningKey.KeyId),
                nameof(PublishedSigningKey.PublicKey),
                nameof(PublishedSigningKey.RetiresAt),
            ],
            typeof(PublishedSigningKey)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .Where(name => name != "EqualityContract")
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            ServiceContracts().SelectMany(Operations),
            operation => operation.Name == nameof(IOidc) + "." + nameof(IOidc.KeysAsync));
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

    /// <summary>
    /// CONV-DESIGN-007 AC6 (D-180): no constructor of a type the library registers has a
    /// parameter whose default stands for an absent declaration, read as any parameter
    /// whose default value is null, whatever its type; and a type whose constructor takes
    /// a declaration that may be absent, read as a parameter of a nullable reference type,
    /// is made by a factory that asks the container for it and is never activated from its
    /// constructor. The registered types are read from the collection the entry point
    /// fills: the type a registration names, the instance it holds, or the type its
    /// factory is declared to answer.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC6_AnAbsentDeclarationReachesItsUserThroughAFactory()
    {
        ServiceDescriptor[] registrations = [.. Registered()];
        Type[] registered =
        [
            .. registrations
                .Select(Made)
                .OfType<Type>()
                .Where(Ships)
                .Distinct(),
        ];
        var nullability = new NullabilityInfoContext();

        IEnumerable<string> defaulted = registered
            .SelectMany(type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.HasDefaultValue && parameter.DefaultValue is null)
                .Select(parameter => type.FullName + " " + parameter.Name));
        IEnumerable<string> activated = registrations
            .Select(Activated)
            .OfType<Type>()
            .Where(Ships)
            .Where(type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => !parameter.ParameterType.IsValueType
                    && nullability.Create(parameter).WriteState is NullabilityState.Nullable))
            .Select(type => type.FullName!);

        Assert.Contains(typeof(KeyRingService), registered);
        Assert.Contains(typeof(Janus.Authentication.Sending.SendingValidation), registered);
        Assert.Empty(defaulted);
        Assert.Empty(activated);
    }

    /// <summary>
    /// CONV-DESIGN-007 AC7: each project that defines a type the container registers
    /// exposes exactly one registration method, named for its project, static and out of
    /// a host's reach, and no other project but the hosting project, whose own is the
    /// entry point, exposes one.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC7_EachProjectExposesItsOneRegistrationMethodAndNoOtherExposesOne()
    {
        IEnumerable<string> exposed = Shipped
            .Where(name => name != Mounting)
            .SelectMany(name => Load(name)
                .GetTypes()
                .SelectMany(type => type.GetMethods(Declared))
                .Where(Registers)
                .Select(method => name + ": " + method.Name + (method.IsStatic && !method.DeclaringType!.IsVisible ? string.Empty : " in reach")));

        Assert.Equal(
            RegistrationMethods.Select(method => method.Key + ": " + method.Value).Order(StringComparer.Ordinal),
            exposed.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC7: the entry point calls the registration method of every other
    /// project, read as every registration such a method makes on its own being among
    /// those the entry point makes, and each method registers the types of its own
    /// project and of no other. Where a factory is declared to answer a contract, the
    /// type it builds is read as the one shipped implementation of that contract.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC7_TheEntryPointCallsEveryMethodAndEachRegistersItsOwnProjectsTypesAlone()
    {
        string[] entry = [.. Registered().Select(Line)];

        Assert.All(RegistrationMethods, method =>
        {
            ServiceDescriptor[] own = [.. Alone(method.Key, method.Value)];

            Assert.NotEmpty(own);
            Assert.Empty(own.Select(Line).Except(entry, StringComparer.Ordinal));
            Assert.Empty(own
                .Select(Built)
                .OfType<Type>()
                .Where(Ships)
                .Where(type => type.Assembly.GetName().Name != method.Key)
                .Select(type => type.FullName));
        });
    }

    /// <summary>
    /// CONV-DESIGN-007 AC7: the shipped defaults that stand in for an absent host
    /// declaration are registered by the core's method, each only where none is
    /// registered, so a host's own declaration is the one the container answers, and
    /// the entry point registers none of them itself. What the entry point registers
    /// itself is read as what its collection holds beyond what the other projects'
    /// methods register on their own.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC7_TheShippedDefaultsAreTheCoreMethodsEachWhereNoneIsRegistered()
    {
        object[] shipped =
        [
            RestrictionKeySuppliers.None,
            PreferenceDeclarations.None,
            ReservedUsernames.Default,
            DictionaryWords.Default,
        ];
        object[] hosts =
        [
            RestrictionKeySuppliers.Of([]),
            PreferenceDeclarations.Of([]),
            ReservedUsernames.Of(["steward"]),
            DictionaryWords.Of(["qx7"]),
        ];
        var declared = new ServiceCollection();

        foreach (object host in hosts)
        {
            _ = declared.AddSingleton(host.GetType(), host);
        }

        using ServiceProvider absent = new ServiceCollection().AddCoreArea().BuildServiceProvider();
        using ServiceProvider present = declared.AddCoreArea().BuildServiceProvider();

        Assert.All(shipped, standing => Assert.Same(standing, absent.GetRequiredService(standing.GetType())));
        Assert.All(hosts, host => Assert.Same(host, Assert.Single(present.GetServices(host.GetType()))));
        Assert.DoesNotContain(Beyond().Select(Made), made => shipped.Any(standing => standing.GetType() == made));
    }

    /// <summary>
    /// CONV-DESIGN-007 AC7: of the types of another project, the entry point registers
    /// itself only the host's declaration it is given and the two built by a factory that
    /// reads a type of a project the type's own cannot reference: the sending validation,
    /// whose placeholders come from the privacy project, and the declared processing,
    /// read from the authorization model. Every other type of such a project is
    /// registered by that project's own method, what evaluates a permission among them
    /// (LIB-SEAM-001 AC1). What the entry point registers itself is read as what its
    /// collection holds beyond what the other projects' methods register on their own.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC7_TheEntryPointRegistersOfAnotherProjectTheDeclarationAndWhatNoOtherCan()
    {
        IEnumerable<Type> others = Beyond()
            .Select(Built)
            .OfType<Type>()
            .Where(Ships)
            .Where(type => type.Assembly.GetName().Name != Mounting)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        Assert.Equal(
            [
                typeof(Janus.Authentication.Sending.SendingValidation),
                typeof(AuthorizationDeclaration),
                typeof(DeclaredProcessing),
            ],
            others);
    }

    /// <summary>
    /// CONV-DESIGN-007 (D-188): a type of the hosting project that bridges two areas is
    /// that project's own, which the entry point registers itself, and it reaches a
    /// member no public contract declares through an internal contract of exactly that
    /// member, which the implementation's own project declares, implements and registers
    /// by its method: the settings restriction over the settings-change gate, and the
    /// mail server's token over the token minting. Neither takes a class of another
    /// project that stands behind a contract.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_ABridgingTypeAsksTheContractTheImplementationsProjectDeclares()
    {
        (Type Bridge, Type Contract, string Member, string Project)[] bridges =
        [
            (
                typeof(Janus.Hosting.Authorization.GatedSettings),
                typeof(Janus.Authorization.Gate.ISettingsChangeGate),
                nameof(Janus.Authorization.Gate.ISettingsChangeGate.RequireSettingsChangeAsync),
                "Janus.Authorization"),
            (
                typeof(Janus.Hosting.Oidc.MailServerTokens),
                typeof(Janus.Authentication.Oidc.ITokenMinting),
                nameof(Janus.Authentication.Oidc.ITokenMinting.MintAsync),
                "Janus.Authentication"),
        ];
        Type?[] own = [.. Beyond().Select(Built)];

        Assert.All(bridges, bridge =>
        {
            Type[] taken =
            [
                .. bridge.Bridge
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(constructor => constructor.GetParameters())
                    .Select(parameter => parameter.ParameterType),
            ];

            Assert.Contains(bridge.Bridge, own);
            Assert.Contains(bridge.Contract, taken);
            Assert.DoesNotContain(taken, type => Ships(type)
                && type.Assembly != bridge.Bridge.Assembly
                && type.IsClass
                && type.GetInterfaces().Any(Ships));
            Assert.True(bridge.Contract is { IsInterface: true, IsVisible: false });
            Assert.Equal([bridge.Member], bridge.Contract.GetMethods().Select(method => method.Name));
            Assert.Equal(bridge.Project, bridge.Contract.Assembly.GetName().Name);
            Assert.Equal(bridge.Project, Assert.Single(Implementations(), bridge.Contract.IsAssignableFrom).Assembly.GetName().Name);
            Assert.Contains(
                Alone(bridge.Project, RegistrationMethods[bridge.Project]),
                service => service.ServiceType == bridge.Contract);
        });
    }

    /// <summary>
    /// CONV-DESIGN-007, CONV-CODE-007: a composition a job builds inside the application
    /// over another credential registers the key ring the start filled, as it stands,
    /// and calls the storage's method and never the core's, whose ring would be a
    /// second, empty one. The audit retention and the restore test build the only two.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AJobsOwnCompositionRegistersTheFilledRingAndNeverCallsTheCoreMethod()
    {
        (string Name, string Text)[] compositions =
        [
            .. Repository
                .Project(Mounting)
                .Select(file => (Name: Path.GetFileName(file), Text: File.ReadAllText(file)))
                .Where(file => file.Text.Contains("new " + nameof(ServiceCollection) + "()", StringComparison.Ordinal))
                .OrderBy(file => file.Name, StringComparer.Ordinal),
        ];

        Assert.Equal(["AuditRetention.cs", "RestoreTest.cs"], compositions.Select(file => file.Name));
        Assert.All(compositions, file =>
        {
            Assert.Contains("IKeyRing ring", file.Text, StringComparison.Ordinal);
            Assert.Contains("services.AddSingleton(ring);", file.Text, StringComparison.Ordinal);
            Assert.Contains("services.AddStorageArea(", file.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("AddCoreArea", file.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// CONV-DESIGN-007 AC8: no project but the hosting project names a namespace of the
    /// shared framework's own, and of the framework's extensions each names the
    /// container's abstractions alone. A project that names them takes them by the
    /// framework reference, and no project takes any of them as a package
    /// (CONV-DESIGN-008).
    /// </summary>
    [Fact]
    public void CONV_DESIGN_007_AC8_OnlyTheHostingProjectUsesTheFrameworkAndTheOthersItsContainerAlone()
    {
        string[] others = [.. Shipped.Where(name => name != Mounting)];

        IEnumerable<string> framework = others
            .SelectMany(Repository.Project)
            .Where(file => FrameworkNamespace.IsMatch(File.ReadAllText(file)));
        IEnumerable<string> extensions = others
            .SelectMany(Repository.Project)
            .SelectMany(file => ExtensionsNamespace
                .Matches(File.ReadAllText(file))
                .Select(named => file + ": " + named.Groups["part"].Value))
            .Where(named => !named.EndsWith(": DependencyInjection", StringComparison.Ordinal));
        IEnumerable<string> unreferenced = Shipped
            .Where(name => Repository.Project(name).Any(file => ExtensionsNamespace.IsMatch(File.ReadAllText(file))))
            .Where(name => !ProjectFile(name)
                .Descendants("FrameworkReference")
                .Any(reference => reference.Attribute("Include")?.Value == SharedFramework));
        IEnumerable<string> packaged = Shipped
            .SelectMany(name => ProjectFile(name)
                .Descendants("PackageReference")
                .Select(reference => reference.Attribute("Include")!.Value)
                .Where(package => package.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                    || package.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal))
                .Select(package => name + ": " + package));

        Assert.Empty(framework);
        Assert.Empty(extensions);
        Assert.Empty(unreferenced);
        Assert.Empty(packaged);
    }

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

    // CONV-DESIGN-007 AC6: what the entry point registers for a deployment that declares
    // what the host fixture declares.
    private static IServiceCollection Registered() =>
        new ServiceCollection().AddJanus(Connection, HostFixture.Declaration(), ApplicationKind.Public);

    // CONV-DESIGN-007 AC7: what the entry point registers itself: what its collection
    // holds once each registration the other projects' methods make on their own has
    // been taken from it, one for one.
    private static List<ServiceDescriptor> Beyond()
    {
        List<ServiceDescriptor> beyond = [.. Registered()];

        foreach (string line in RegistrationMethods.SelectMany(method => Alone(method.Key, method.Value)).Select(Line))
        {
            int at = beyond.FindIndex(service => Line(service) == line);

            if (at >= 0)
            {
                beyond.RemoveAt(at);
            }
        }

        return beyond;
    }

    // CONV-DESIGN-007 AC7: a registration method takes the collection it adds to first.
    private static bool Registers(MethodInfo method) =>
        method.GetParameters() is [ParameterInfo first, ..] && first.ParameterType == typeof(IServiceCollection);

    // CONV-DESIGN-007 AC7: what one project's method registers on its own, given what the
    // entry point is given.
    private static ServiceCollection Alone(string project, string name)
    {
        MethodInfo registration = Load(project)
            .GetTypes()
            .SelectMany(type => type.GetMethods(Declared))
            .Single(method => method.Name == name && Registers(method));
        var services = new ServiceCollection();

        _ = registration.Invoke(
            null,
            [
                .. registration.GetParameters().Select(parameter =>
                    parameter.ParameterType == typeof(IServiceCollection) ? services
                    : parameter.ParameterType == typeof(AuthorizationDeclaration) ? HostFixture.Declaration()
                    : (object)Connection),
            ]);

        return services;
    }

    // One registration: what it is asked for, how long it lives and what it makes.
    private static string Line(ServiceDescriptor service) =>
        service.ServiceType.FullName + " " + service.Lifetime + " " + Made(service)?.FullName;

    // CONV-DESIGN-007 AC7: the type a registration builds: the one it makes, or, where its
    // factory is declared to answer a contract, the one shipped implementation of that
    // contract.
    private static Type? Built(ServiceDescriptor service) =>
        Made(service) is { IsInterface: true } contract
            ? Implementations().Where(contract.IsAssignableFrom).ToArray() is [Type single] ? single : null
            : Made(service);

    // A project's own file, as the repository lays it out.
    private static XDocument ProjectFile(string project) =>
        XDocument.Load(Path.Combine(Repository.Root(), "src", project, project + ".csproj"));

    // The type a registration makes: the one it names, the instance it holds, or what its
    // factory is declared to answer.
    private static Type? Made(ServiceDescriptor service) =>
        service.IsKeyedService
            ? service.KeyedImplementationType
                ?? service.KeyedImplementationInstance?.GetType()
                ?? service.KeyedImplementationFactory?.Method.ReturnType
            : service.ImplementationType
                ?? service.ImplementationInstance?.GetType()
                ?? service.ImplementationFactory?.Method.ReturnType;

    private static bool Ships(Type type) => Shipped.Contains(type.Assembly.GetName().Name, StringComparer.Ordinal);

    // The type the container makes from its constructor, where a registration names one.
    private static Type? Activated(ServiceDescriptor service) =>
        service.IsKeyedService ? service.KeyedImplementationType : service.ImplementationType;

    // CONV-DESIGN-002 AC1: every public interface of the core but those the host
    // implements.
    private static Type[] ServiceContracts() =>
    [
        .. typeof(Result).Assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface)
            .Where(type => !NotServiceContracts.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type)),
    ];

    // CONV-DESIGN-004 AC2: the folder of every project of the repository: the areas,
    // the storage, the contract, the mounting, the command line, the conformance suite,
    // the analysers and the tools alike.
    private static IEnumerable<string> EveryProject() =>
        ProjectRoots
            .SelectMany(root => Directory.GetFiles(
                Path.Combine(Repository.Root(), root),
                "*.csproj",
                SearchOption.AllDirectories))
            .Select(project => Path.GetDirectoryName(project)!);

    // Every file written for a project, leaving out what the build writes.
    private static IEnumerable<string> Sources(string project) =>
        Directory
            .GetFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(folder => folder is "bin" or "obj"));

    // CONV-DESIGN-004 AC2: whether the file declares the typed identifier or value it is
    // named for, wrapping the type a parameter was matched as.
    private static bool MadeFrom(string type, string text, string wrapped) =>
        Regex.IsMatch(
            text,
            @"\breadonly\s+record\s+struct\s+" + Regex.Escape(type) + @"\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5))
        && Wrapped.Matches(text).Any(value => value.Groups["wrapped"].Value == wrapped);

    // CONV-DESIGN-004 AC2: the member whose parameter list holds the place given: the
    // name before the bracket that list opens with.
    private static string Taking(string text, int at)
    {
        int depth = 0;
        int open = at;

        while (open > 0 && !(text[open] == '(' && depth == 0))
        {
            depth += text[open] switch
            {
                ')' => 1,
                '(' => -1,
                _ => 0,
            };
            open--;
        }

        int end = open;

        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        int start = end;

        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return text[start..end];
    }

    // CONV-DESIGN-004 AC2: an assembly a listed or named package ships under its own
    // name, or one of the provider's, whose listed packages bring its abstractions.
    private static bool OfAPackage(string assembly, string[] packages) =>
        packages.Contains(assembly, StringComparer.Ordinal)
        || assembly.StartsWith("OpenIddict.", StringComparison.Ordinal);

    private static IEnumerable<Type> Implementations() => ShippedTypes
        .Where(type => type is { IsClass: true, IsAbstract: false })
        .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    // LIB-API-005: the operations of a contract are its methods taking an access
    // context, each named with its contract and paired with the method implementing it.
    private static IEnumerable<(string Name, MethodInfo Implementing)> Operations(Type contract)
    {
        InterfaceMapping map = Implementations().Single(contract.IsAssignableFrom).GetInterfaceMap(contract);

        return map.InterfaceMethods
            .Select((method, at) => (Method: method, Implementing: map.TargetMethods[at]))
            .Where(pair => pair.Method.GetParameters().Any(parameter => parameter.ParameterType == typeof(AccessContext)))
            .Select(pair => (contract.Name + "." + pair.Method.Name, pair.Implementing));
    }

    // CONV-DESIGN-002 AC3: what a method does before it meets the gate, or nothing where
    // the gate comes first. Every declaration of the name is read where the parameters
    // are not known, and each must meet it.
    private static string? BeforeTheGate(Type type, string method, string[]? parameters, bool own, int depth)
    {
        if (depth > 6)
        {
            return type.Name + "." + method + " hands the caller on further than is read";
        }

        string text = Source(type);
        string[] bodies = [.. Bodies(text, method, parameters)];

        return bodies.Length == 0
            ? type.Name + "." + method + " is not declared where its type is written"
            : bodies
                .Select(body => FirstStep(type, Collaborators(text, type), body, own, depth))
                .FirstOrDefault(unmet => unmet is not null);
    }

    // The first call a body makes, past the resolution of its scope: the gate, a method
    // handed the caller and followed, or what the body does before the gate. An
    // operation on the caller's own records may ask whose account is asking first.
    private static string? FirstStep(
        Type type,
        Dictionary<string, string> collaborators,
        string body,
        bool own,
        int depth)
    {
        Match? first = Call.Matches(body).FirstOrDefault(call => call.Groups["name"].Value != ScopeResolution);
        int at = first?.Index ?? body.Length;

        if (own && Identity.IsMatch(body[..at]))
        {
            return null;
        }

        if (first is null)
        {
            return type.Name + " makes no call to the gate";
        }

        string target = first.Groups["target"].Value;
        string name = first.Groups["name"].Value;
        bool handsOn = Caller.IsMatch(Enclosed(body, first.Index + first.Length - 1));

        if (collaborators.TryGetValue(target, out string? kind) && GateSteps.Contains(kind))
        {
            return null;
        }

        if (handsOn && target.Length == 0)
        {
            return BeforeTheGate(type, name, parameters: null, own, depth + 1);
        }

        if (handsOn && kind is not null && Implementation(kind) is Type next)
        {
            return BeforeTheGate(next, name, parameters: null, own, depth + 1);
        }

        return type.Name + " calls " + (target.Length == 0 ? name : target + "." + name) + " first";
    }

    // The type's file, as the repository lays it out, with its comments taken out.
    private static string Source(Type type)
    {
        string name = type.Name.Split('`')[0];

        return Comment.Replace(
            File.ReadAllText(Repository
                .Project(type.Assembly.GetName().Name!)
                .Single(path => Path.GetFileNameWithoutExtension(path) == name)),
            string.Empty);
    }

    // What the type is constructed with, by the name it is held under.
    private static Dictionary<string, string> Collaborators(string text, Type type)
    {
        Match declared = Regex.Match(
            text,
            @"\bclass\s+" + type.Name.Split('`')[0] + @"\s*(?:<[^<>]*>)?\s*\(",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

        return declared.Success
            ? Split(Enclosed(text, declared.Index + declared.Length - 1))
                .Select(parameter => Parameter.Match(parameter.Trim()))
                .Where(parameter => parameter.Success)
                .ToDictionary(
                    parameter => parameter.Groups["name"].Value,
                    parameter => parameter.Groups["type"].Value.Split('.')[^1],
                    StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    // The one implementation of a collaborator the type is constructed with.
    private static Type? Implementation(string kind)
    {
        Type[] named = [.. ShippedTypes.Where(type => type.Name == kind)];

        if (named is not [Type declared])
        {
            return null;
        }

        Type[] implementing = declared.IsInterface
            ? [.. Implementations().Where(declared.IsAssignableFrom)]
            : [declared];

        return implementing is [Type single] ? single : null;
    }

    // The bodies of the methods of one name, as the file writes them; where the
    // parameters are known, of the one declaration taking them.
    private static IEnumerable<string> Bodies(string text, string method, string[]? parameters)
    {
        MatchCollection declarations = Regex.Matches(
            text,
            @"^[ \t]*(?:(?:public|private|internal|protected|static|async|override)\s+)+[^\n;{}=]*?\b"
                + method
                + @"\s*(?:<[^<>()]*>)?\s*\(",
            RegexOptions.CultureInvariant | RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        foreach (Match declaration in declarations)
        {
            int open = declaration.Index + declaration.Length - 1;
            string list = Enclosed(text, open);
            int after = open + list.Length + 2;
            int block = Ahead(text.IndexOf('{', after));
            int arrow = Ahead(text.IndexOf("=>", after, StringComparison.Ordinal));

            if (Ahead(text.IndexOf(';', after)) < Math.Min(block, arrow))
            {
                continue;
            }

            if (parameters is not null && !Split(list).Select(NameOf).SequenceEqual(parameters))
            {
                continue;
            }

            yield return arrow >= 0 && arrow < block
                ? text[(arrow + 2)..Statement(text, arrow + 2)]
                : Enclosed(text, block);
        }
    }

    // Where a search found what it looked for, or past the end where it found nothing.
    private static int Ahead(int found) => found < 0 ? int.MaxValue : found;

    // The name a parameter is declared under, before any default it carries.
    private static string NameOf(string parameter) =>
        parameter.Split('=')[0].Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[^1];

    // What an opening bracket encloses, up to the one that closes it.
    private static string Enclosed(string text, int open)
    {
        int depth = 0;

        for (int at = open; at < text.Length; at++)
        {
            depth += text[at] switch
            {
                '(' or '{' or '[' => 1,
                ')' or '}' or ']' => -1,
                _ => 0,
            };

            if (depth == 0)
            {
                return text[(open + 1)..at];
            }
        }

        return text[(open + 1)..];
    }

    // Where the statement starting at a position ends.
    private static int Statement(string text, int start)
    {
        int depth = 0;

        for (int at = start; at < text.Length; at++)
        {
            depth += text[at] switch
            {
                '(' or '{' or '[' => 1,
                ')' or '}' or ']' => -1,
                _ => 0,
            };

            if (depth == 0 && text[at] == ';')
            {
                return at;
            }
        }

        return text.Length;
    }

    // A list split at its own commas, leaving those inside brackets whole.
    private static IEnumerable<string> Split(string list)
    {
        int depth = 0;
        int start = 0;

        for (int at = 0; at < list.Length; at++)
        {
            depth += list[at] switch
            {
                '(' or '<' or '[' => 1,
                ')' or '>' or ']' => -1,
                _ => 0,
            };

            if (depth == 0 && list[at] == ',')
            {
                yield return list[start..at];
                start = at + 1;
            }
        }

        if (list[start..].Trim().Length > 0)
        {
            yield return list[start..];
        }
    }

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
