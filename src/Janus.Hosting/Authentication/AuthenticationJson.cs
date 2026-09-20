using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Authentication;

/// <summary>
/// How authentication reads and writes, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SignInRequest))]
[JsonSerializable(typeof(PresentFactorRequest))]
[JsonSerializable(typeof(VerifyDeviceRequest))]
[JsonSerializable(typeof(AbandonLinkRequest))]
[JsonSerializable(typeof(SignInChallengeView))]
[JsonSerializable(typeof(SignInProgressView))]
[JsonSerializable(typeof(SignInLandingView))]
[JsonSerializable(typeof(SessionDetailView))]
[JsonSerializable(typeof(IReadOnlyList<DeviceView>))]
internal sealed partial class AuthenticationJson : JsonSerializerContext;
