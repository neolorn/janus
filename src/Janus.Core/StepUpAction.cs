using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// An action on the library's own surface that requires step-up under the principal's
/// policy. The name is the key of a policy's gates.
/// </summary>
/// <remarks>Implements chapter 10 section 5a, AUTH-STEP-001, AUTH-STEP-002.</remarks>
public enum StepUpAction
{
    /// <summary>
    /// Set or change a password.
    /// </summary>
    /// <remarks>Named <c>password:set</c>.</remarks>
    [JsonStringEnumMemberName("password:set")]
    PasswordSet = 0,

    /// <summary>
    /// Add an identifier, or replace one in single-address mode.
    /// </summary>
    /// <remarks>Named <c>identifier:add</c>.</remarks>
    [JsonStringEnumMemberName("identifier:add")]
    IdentifierAdd = 1,

    /// <summary>
    /// Remove an identifier. The undo is link-borne and not gated.
    /// </summary>
    /// <remarks>Named <c>identifier:remove</c>.</remarks>
    [JsonStringEnumMemberName("identifier:remove")]
    IdentifierRemove = 2,

    /// <summary>
    /// Change the username. Display name and legal name are not gated.
    /// </summary>
    /// <remarks>Named <c>username:change</c>.</remarks>
    [JsonStringEnumMemberName("username:change")]
    UsernameChange = 3,

    /// <summary>
    /// Enrol a factor, or upgrade a security key to a passkey.
    /// </summary>
    /// <remarks>Named <c>factor:enrol</c>.</remarks>
    [JsonStringEnumMemberName("factor:enrol")]
    FactorEnrol = 4,

    /// <summary>
    /// Remove an active factor.
    /// </summary>
    /// <remarks>Named <c>factor:remove</c>.</remarks>
    [JsonStringEnumMemberName("factor:remove")]
    FactorRemove = 5,

    /// <summary>
    /// Generate or regenerate recovery codes.
    /// </summary>
    /// <remarks>Named <c>recoverycodes:generate</c>.</remarks>
    [JsonStringEnumMemberName("recoverycodes:generate")]
    RecoveryCodesGenerate = 6,

    /// <summary>
    /// Create a mail app password.
    /// </summary>
    /// <remarks>Named <c>mailcredential:create</c>.</remarks>
    [JsonStringEnumMemberName("mailcredential:create")]
    MailCredentialCreate = 7,

    /// <summary>
    /// Revoke a mail app password.
    /// </summary>
    /// <remarks>Named <c>mailcredential:revoke</c>.</remarks>
    [JsonStringEnumMemberName("mailcredential:revoke")]
    MailCredentialRevoke = 8,

    /// <summary>
    /// Export personal data.
    /// </summary>
    /// <remarks>Named <c>privacy:export</c>.</remarks>
    [JsonStringEnumMemberName("privacy:export")]
    PrivacyExport = 9,

    /// <summary>
    /// Request account deletion.
    /// </summary>
    /// <remarks>Named <c>account:delete</c>.</remarks>
    [JsonStringEnumMemberName("account:delete")]
    AccountDelete = 10,

    /// <summary>
    /// Deactivate the account. Reactivation from the notice is not gated.
    /// </summary>
    /// <remarks>Named <c>account:deactivate</c>.</remarks>
    [JsonStringEnumMemberName("account:deactivate")]
    AccountDeactivate = 11,

    /// <summary>
    /// Link a social provider.
    /// </summary>
    /// <remarks>Named <c>provider:link</c>.</remarks>
    [JsonStringEnumMemberName("provider:link")]
    ProviderLink = 12,

    /// <summary>
    /// Unlink a social provider.
    /// </summary>
    /// <remarks>Named <c>provider:unlink</c>.</remarks>
    [JsonStringEnumMemberName("provider:unlink")]
    ProviderUnlink = 13,

    /// <summary>
    /// Approve a recovery.
    /// </summary>
    /// <remarks>Named <c>recovery:approve</c>.</remarks>
    [JsonStringEnumMemberName("recovery:approve")]
    RecoveryApprove = 14,

    /// <summary>
    /// Issue an invitation.
    /// </summary>
    /// <remarks>Named <c>invitation:issue</c>.</remarks>
    [JsonStringEnumMemberName("invitation:issue")]
    InvitationIssue = 15,

    /// <summary>
    /// Grant, revoke or change roles and grants.
    /// </summary>
    /// <remarks>Named <c>grant:manage</c>.</remarks>
    [JsonStringEnumMemberName("grant:manage")]
    GrantManage = 16,

    /// <summary>
    /// Suspend an account.
    /// </summary>
    /// <remarks>Named <c>account:suspend</c>.</remarks>
    [JsonStringEnumMemberName("account:suspend")]
    AccountSuspend = 17,

    /// <summary>
    /// Reactivate a suspended account.
    /// </summary>
    /// <remarks>Named <c>account:reactivate</c>.</remarks>
    [JsonStringEnumMemberName("account:reactivate")]
    AccountReactivate = 18,

    /// <summary>
    /// Execute a takedown.
    /// </summary>
    /// <remarks>Named <c>account:takedown</c>.</remarks>
    [JsonStringEnumMemberName("account:takedown")]
    AccountTakedown = 19,

    /// <summary>
    /// Reverse a takedown.
    /// </summary>
    /// <remarks>Named <c>account:takedownreverse</c>.</remarks>
    [JsonStringEnumMemberName("account:takedownreverse")]
    AccountTakedownReverse = 20,

    /// <summary>
    /// Complete a stuck erasure manually.
    /// </summary>
    /// <remarks>Named <c>erasure:complete</c>.</remarks>
    [JsonStringEnumMemberName("erasure:complete")]
    ErasureComplete = 21,

    /// <summary>
    /// Loosen a security setting. A tightening is not gated.
    /// </summary>
    /// <remarks>Named <c>config:loosen</c>.</remarks>
    [JsonStringEnumMemberName("config:loosen")]
    ConfigLoosen = 22,

    /// <summary>
    /// Change alert destinations.
    /// </summary>
    /// <remarks>Named <c>alerting:destinations</c>.</remarks>
    [JsonStringEnumMemberName("alerting:destinations")]
    AlertingDestinations = 23,

    /// <summary>
    /// Change an organization's policy.
    /// </summary>
    /// <remarks>Named <c>policy:change</c>.</remarks>
    [JsonStringEnumMemberName("policy:change")]
    PolicyChange = 24,

    /// <summary>
    /// Add, verify or remove a locked domain.
    /// </summary>
    /// <remarks>Named <c>domain:manage</c>.</remarks>
    [JsonStringEnumMemberName("domain:manage")]
    DomainManage = 25,

    /// <summary>
    /// Edit a sending restriction. A loosening also needs a reason.
    /// </summary>
    /// <remarks>Named <c>restriction:edit</c>.</remarks>
    [JsonStringEnumMemberName("restriction:edit")]
    RestrictionEdit = 26,

    /// <summary>
    /// Grant sends to a key, with a reason.
    /// </summary>
    /// <remarks>Named <c>restriction:grant</c>.</remarks>
    [JsonStringEnumMemberName("restriction:grant")]
    RestrictionGrant = 27,

    /// <summary>
    /// Generate a replacement break-glass credential.
    /// </summary>
    /// <remarks>Named <c>breakglass:replace</c>.</remarks>
    [JsonStringEnumMemberName("breakglass:replace")]
    BreakGlassReplace = 28,

    /// <summary>
    /// Request an organization's deletion, which suspends it and ends the sessions of
    /// its members, or cancel the request, which gives back every grant it holds.
    /// </summary>
    /// <remarks>Named <c>organization:delete</c>.</remarks>
    [JsonStringEnumMemberName("organization:delete")]
    OrganizationDelete = 29,
}
