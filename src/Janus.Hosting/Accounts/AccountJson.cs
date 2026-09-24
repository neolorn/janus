using System.Text.Json.Serialization;

namespace Janus.Hosting.Accounts;

/// <summary>
/// How the account application reads and writes, generated rather than reflected over
/// (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements API-CONV-002 and CONV-DESIGN-006.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AccountView))]
[JsonSerializable(typeof(ProfileEditRequest))]
[JsonSerializable(typeof(PreferencesView))]
[JsonSerializable(typeof(PreferencesRequest))]
[JsonSerializable(typeof(AddIdentifierToAccountRequest))]
[JsonSerializable(typeof(VerifyIdentifierRequest))]
[JsonSerializable(typeof(LinkTokenRequest))]
[JsonSerializable(typeof(BackupRequest))]
[JsonSerializable(typeof(ReplaceIdentifierRequest))]
[JsonSerializable(typeof(LabelRequest))]
[JsonSerializable(typeof(PreferredSecondStepRequest))]
[JsonSerializable(typeof(IdentifierLandingView))]
[JsonSerializable(typeof(DeletionView))]
[JsonSerializable(typeof(AttachedInvitationView))]
[JsonSerializable(typeof(AcknowledgeInvitationRequest))]
[JsonSerializable(typeof(AppPasswordRequest))]
[JsonSerializable(typeof(IssuedAppPasswordView))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<AppPasswordView>))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<CredentialView>))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<SessionView>))]
internal sealed partial class AccountJson : JsonSerializerContext;
