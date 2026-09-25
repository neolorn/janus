using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Callbacks;
using Janus.Hosting.Tests.Bff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the shipped assemblies may expose, derive from and carry: public types in the
/// contract's projects alone, one internal sealed implementation of each service
/// contract whose operations meet the gate first, one public registration, no
/// controller, and no validation attribute (LIB-API-002, CONV-LAYOUT-002,
/// CONV-DESIGN-002, CONV-DESIGN-006, CONV-DESIGN-007, CONV-CODE-006).
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
        nameof(IConsents) + "." + nameof(IConsents.GrantAsync),
        nameof(IConsents) + "." + nameof(IConsents.ObjectAsync),
        nameof(IConsents) + "." + nameof(IConsents.ObjectionsAsync),
        nameof(IConsents) + "." + nameof(IConsents.ReadAsync),
        nameof(IConsents) + "." + nameof(IConsents.WithdrawAsync),
        nameof(IConsents) + "." + nameof(IConsents.WithdrawObjectionAsync),
        nameof(IExports) + "." + nameof(IExports.AssembleAsync),
        nameof(IInvitations) + "." + nameof(IInvitations.AcknowledgeAsync),
        nameof(IInvitations) + "." + nameof(IInvitations.AttachedAsync),
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
    // on its context before anything else (REG-SESS-002). The gate itself is left out,
    // since it is the gate.
    private static readonly string[] Ungated =
    [
        nameof(IDerivationMaterialiser) + "." + nameof(IDerivationMaterialiser.RefreshAsync),
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

    // CONV-DESIGN-002 AC1: every public interface of the core but those the host
    // implements.
    private static Type[] ServiceContracts() =>
    [
        .. typeof(Result).Assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface)
            .Where(type => !NotServiceContracts.Contains(type.IsGenericType ? type.GetGenericTypeDefinition() : type)),
    ];

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
