using System.Collections.Generic;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Storage.Authentication.Events;

/// <summary>
/// How an emitted event and the consumers that took it are written and read, generated
/// rather than reflected over (CONV-CODE-004). A value of the vocabulary is written as
/// its name, so a row outlives a reordering of the enumeration.
/// </summary>
/// <remarks>Implements LIB-API-001 and chapter 10 section 5b.</remarks>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(AccountDeletionCancelled))]
[JsonSerializable(typeof(AccountDeletionRequested))]
[JsonSerializable(typeof(AccountReactivated))]
[JsonSerializable(typeof(AccountRegistered))]
[JsonSerializable(typeof(AccountSuspended))]
[JsonSerializable(typeof(AlertRaised))]
[JsonSerializable(typeof(ConsentChanged))]
[JsonSerializable(typeof(CredentialEnrolled))]
[JsonSerializable(typeof(CredentialInvalidated))]
[JsonSerializable(typeof(CredentialRestored))]
[JsonSerializable(typeof(CredentialSuspended))]
[JsonSerializable(typeof(DeviceVerified))]
[JsonSerializable(typeof(ErasureRequested))]
[JsonSerializable(typeof(ExportRequested))]
[JsonSerializable(typeof(IdentifierAdded))]
[JsonSerializable(typeof(IdentifierPrimaryChanged))]
[JsonSerializable(typeof(IdentifierRemoved))]
[JsonSerializable(typeof(MembershipChanged))]
[JsonSerializable(typeof(NotificationRequested))]
[JsonSerializable(typeof(ObjectionChanged))]
[JsonSerializable(typeof(OrganizationErased))]
[JsonSerializable(typeof(RestrictionChanged))]
[JsonSerializable(typeof(SendingRestrictionChanged))]
[JsonSerializable(typeof(SendingRestrictionGranted))]
[JsonSerializable(typeof(TakedownExecuted))]
[JsonSerializable(typeof(TakedownReversed))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class EventJson : JsonSerializerContext;
