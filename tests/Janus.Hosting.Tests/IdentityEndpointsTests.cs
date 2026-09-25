using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the endpoints the library mounts are to its contract: each resolves one service
/// of it, carries no logic that service does not, and checks no permission of its own
/// (LIB-API-005).
/// </summary>
/// <remarks>
/// Each endpoint is named by its methods and its route as the deployment mounts it, and
/// its handler is read from the route's metadata. What a handler does is read from its
/// compiled body and from the bodies of the methods of its own endpoint class it reaches,
/// which is where a mapping would grow logic of its own; what it hands to another type
/// of the host is that type's business.
/// </remarks>
[Trait("kind", "contract")]
public sealed class IdentityEndpointsTests
{
    // The endpoints that are not an operation of the contract, and so are outside the
    // rule. The protocol endpoints of the provider are not in the list because the
    // provider's middleware serves them and the deployment mounts no route for them;
    // the two that answer from the contract do so from IOidc (entry 160).
    private static readonly string[] Outside =
    [
        // Establishing the browser's session, which is the boundary's own work: the
        // session and its cookie are what the browser holds, not a result a host
        // calling in process could use (BFF-SESS-001, BFF-SESS-006).
        "GET /auth/providers/apple",
        "GET /auth/providers/apple/return",
        "GET /auth/providers/google",
        "GET /auth/providers/google/return",
        "GET /auth/signon",
        "GET /auth/signon/return",
        "GET,POST /callbacks/providers/apple/return",
        "GET,POST /callbacks/providers/google/return",
        "POST /auth/break-glass",
        "POST /auth/device/verify",
        "POST /auth/factor",
        "POST /auth/step-up",
        "POST /register/terms",

        // The break-glass credential, whose service entry 297 keeps off the contract.
        "POST /admin/break-glass/generate",

        // What a gateway or a provider sends, which no person and no host calls
        // (chapter 09 section 10, IDN-LIFE-012a AC3, entry 282).
        "GET /callbacks/sms/dlr",
        "POST /callbacks/providers/apple",
        "POST /callbacks/providers/google",

        // The documents at the site root, which answer the host's declarations and its
        // settings and perform nothing (REG-PM-001, AUTH-FACT-012).
        "GET /.well-known/change-password",
        "GET /.well-known/passkey-endpoints",
        "GET /.well-known/webauthn",
    ];

    // A public interface of Janus.Core an endpoint may read that is not an operation:
    // the settings, read as every part of the library reads them.
    private static readonly Type[] NotOperations = [typeof(IConfigurationStore)];

    // The assemblies holding the services, which an endpoint reaches only through the
    // contract.
    private static readonly string[] Areas =
    [
        "Janus.Authentication",
        "Janus.Authorization",
        "Janus.Identity",
        "Janus.Privacy",
        "Janus.Storage",
    ];

    // What an endpoint may take from an area besides the contract, all of it the
    // browser's rather than the operation's: its pre-authentication record, which binds
    // a registration or an enrolment to the browser that started it (BFF-CSRF-005b,
    // AUTH-RECOV-002); the rotation of its session when an identifier change completes
    // (entry 363); and the wake of the registration's event stream, which reads the
    // state only through the contract (REG-SESS-003).
    private static readonly Type[] TheBrowsers =
        [typeof(PreAuthenticationService), typeof(SessionService), typeof(IRegistrationSignals)];

    private static readonly string[] BrowserOperations =
        [nameof(PreAuthenticationService.CarryAsync), nameof(SessionService.RotateAsync), nameof(IRegistrationSignals.WaitAsync)];

    // The members of the gate that decide whether something is allowed, as against the
    // operations of the contract it also carries (reverse lookup, explanation
    // resolution).
    private static readonly string[] Deciding =
    [
        nameof(IAccessGate.CapabilitiesAsync),
        nameof(IAccessGate.ExplainAsync),
        nameof(IAccessGate.FilterAsync),
        nameof(IAccessGate.FragmentAsync),
        nameof(IAccessGate.RequireAsync),
    ];

    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>
    /// LIB-API-005 AC3: every endpoint that is an operation takes exactly one service of
    /// the contract and nothing of an area but what belongs to the browser, so an
    /// operation added without its service in Janus.Core fails here.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_API_005_AC3_EveryEndpointResolvesExactlyOneServiceOfTheContractAsync()
    {
        await using var deployment = new Deployment();
        Dictionary<string, MethodInfo> handlers = Handlers(deployment);

        Assert.Empty(Outside.Except(handlers.Keys, StringComparer.Ordinal));
        Assert.Empty(Operations(handlers).Where(pair => !ResolvesOneService(pair.Value)).Select(pair => pair.Key));
    }

    /// <summary>
    /// LIB-API-005 AC1: no endpoint carries logic its service does not. What a handler
    /// and the methods of its class reach of the library is one contract, the browser's
    /// own records, and the values the contract is written in; no service of an area is
    /// called past the contract and no second contract is called beside the first.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_API_005_AC1_NoEndpointCarriesLogicItsServiceDoesNotAsync()
    {
        await using var deployment = new Deployment();

        Assert.Empty(Operations(Handlers(deployment))
            .Where(pair => CarriesLogic(Reached(pair.Value)))
            .Select(pair => pair.Key));
    }

    /// <summary>
    /// LIB-API-005 AC2: no endpoint the library mounts, and nothing of the pipeline in
    /// front of them, checks a permission. The check a request over HTTP meets is the
    /// service's own, so a host calling the service in process meets the same one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_API_005_AC2_NoEndpointChecksAPermissionOfItsOwnAsync()
    {
        await using var deployment = new Deployment();
        Dictionary<string, MethodInfo> handlers = Handlers(deployment);

        Assert.NotEmpty(handlers);
        Assert.Empty(handlers.Where(pair => Reached(pair.Value).Any(Decides)).Select(pair => pair.Key));

        MethodBase[] pipeline =
        [
            .. typeof(IdentityEndpoints).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "Janus.Hosting.Bff")
                .SelectMany(Declared),
        ];

        Assert.NotEmpty(pipeline);
        Assert.Empty(pipeline
            .Where(method => Named(method).Any(Decides))
            .Select(method => method.DeclaringType + "." + method.Name));
    }

    // Every endpoint the deployment mounts that carries a handler, by its methods and
    // its route.
    private static Dictionary<string, MethodInfo> Handlers(Deployment deployment)
    {
        var handlers = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        foreach (RouteEndpoint endpoint in deployment.Endpoints.OfType<RouteEndpoint>())
        {
            MethodInfo? handler = endpoint.Metadata.GetMetadata<MethodInfo>() ?? endpoint.RequestDelegate?.Method;
            string methods = string.Join(",", endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []);

            if (handler is not null)
            {
                handlers.Add(methods + " " + endpoint.RoutePattern.RawText, handler);
            }
        }

        return handlers;
    }

    private static IEnumerable<KeyValuePair<string, MethodInfo>> Operations(Dictionary<string, MethodInfo> handlers) =>
        handlers.Where(pair => !Outside.Contains(pair.Key, StringComparer.Ordinal));

    // Exactly one service of the contract among what the handler takes, and nothing of
    // an area but the browser's.
    private static bool ResolvesOneService(MethodInfo handler)
    {
        Type[] taken = [.. handler.GetParameters().Select(parameter => parameter.ParameterType)];

        return taken.Where(IsContract).Distinct().Count() == 1
            && !taken.Any(type => IsArea(type) && !TheBrowsers.Contains(type));
    }

    // A service of the area reached past the contract, or a second contract beside the
    // first. A property read of an area's record (the browser's session, say) is a value
    // and not logic.
    private static bool CarriesLogic(IReadOnlyList<MethodBase> reached) =>
        reached.Any(called => called.DeclaringType is { } declaring
            && IsArea(declaring)
            && !(called.IsSpecialName && called.Name.StartsWith("get_", StringComparison.Ordinal))
            && !(TheBrowsers.Contains(declaring) && BrowserOperations.Contains(called.Name, StringComparer.Ordinal)))
        || reached
            .Select(called => called.DeclaringType)
            .Where(declaring => declaring is not null && IsContract(declaring))
            .Distinct()
            .Count() > 1;

    // A permission named, or the gate asked whether something is allowed.
    private static bool Decides(MethodBase called) =>
        called.DeclaringType is { } declaring
        && (declaring == typeof(Permissions)
            || (typeof(IAccessGate).IsAssignableFrom(declaring)
                && Deciding.Contains(called.Name, StringComparer.Ordinal)));

    private static bool IsContract(Type type) =>
        type.IsInterface
        && type.IsPublic
        && type.Assembly == typeof(IAccessGate).Assembly
        && !NotOperations.Contains(type);

    private static bool IsArea(Type type) =>
        Areas.Contains(type.Assembly.GetName().Name, StringComparer.Ordinal);

    // Every method the handler names, and every method named by a method of its own
    // endpoint class it reaches: lambdas, local functions and helpers alike.
    private static List<MethodBase> Reached(MethodInfo handler)
    {
        Type owner = Outermost(handler.DeclaringType!);
        var read = new HashSet<MethodBase>();
        var reached = new List<MethodBase>();
        var pending = new Stack<MethodBase>([handler]);

        while (pending.TryPop(out MethodBase? method))
        {
            if (!read.Add(method))
            {
                continue;
            }

            foreach (MethodBase named in Named(method))
            {
                reached.Add(named);

                if (named.DeclaringType is { } declaring && Outermost(declaring) == owner)
                {
                    pending.Push(named);
                }
            }
        }

        return reached;
    }

    // Every method a body calls, constructs or takes as a delegate, the body of an
    // async or iterator method being its state machine's.
    private static IEnumerable<MethodBase> Named(MethodBase method)
    {
        MethodBase body = method.GetCustomAttribute<StateMachineAttribute>() is { } machine
            ? machine.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!
            : method;

        if (body.GetMethodBody()?.GetILAsByteArray() is not { } il)
        {
            yield break;
        }

        Type[] typeArguments = body.DeclaringType is { IsGenericType: true } generic
            ? generic.GetGenericArguments()
            : Type.EmptyTypes;
        Type[] methodArguments = body.IsGenericMethod ? body.GetGenericArguments() : Type.EmptyTypes;

        for (int at = 0; at < il.Length;)
        {
            OpCode code = il[at] == 0xFE ? Codes[unchecked((short)(0xFE00 | il[at + 1]))] : Codes[il[at]];

            at += code.Size;

            if (code.OperandType == OperandType.InlineMethod)
            {
                yield return body.Module.ResolveMethod(BitConverter.ToInt32(il, at), typeArguments, methodArguments)!;
            }

            at += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, at)),
                _ => 4,
            };
        }
    }

    private static IEnumerable<MethodBase> Declared(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

    private static Type Outermost(Type type)
    {
        while (type.DeclaringType is { } declaring)
        {
            type = declaring;
        }

        return type;
    }
}
