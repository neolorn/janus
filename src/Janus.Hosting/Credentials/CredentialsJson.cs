using System.Text.Json.Serialization;

namespace Janus.Hosting.Credentials;

/// <summary>
/// How the credential endpoints read and write, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SetPasswordRequest))]
[JsonSerializable(typeof(BeginKeyRequest))]
[JsonSerializable(typeof(CompleteKeyRequest))]
[JsonSerializable(typeof(GeneratorRequest))]
[JsonSerializable(typeof(ConfirmGeneratorRequest))]
[JsonSerializable(typeof(CredentialCeremonyView))]
[JsonSerializable(typeof(EnrolledCredentialView))]
[JsonSerializable(typeof(GeneratorEnrolmentView))]
[JsonSerializable(typeof(RecoveryCodesView))]
internal sealed partial class CredentialsJson : JsonSerializerContext;
