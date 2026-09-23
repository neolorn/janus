using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Organizations;

/// <summary>
/// The policy an organization's members resolve to, each field and each gate marked
/// with whether its value is the organization's own or inherited.
/// </summary>
/// <param name="RequiredAssurance">The assurance floor.</param>
/// <param name="LoginFactors">The factors a member may sign in with.</param>
/// <param name="Gates">What each step-up action costs, by the action's name.</param>
/// <param name="CredentialRedundancy">Whether a second credential is required.</param>
/// <param name="SelfServiceRecovery">Whether recovery is self-service.</param>
/// <param name="EmailDomains">The domain lock; empty is off.</param>
/// <remarks>Implements chapter 09 section 8a, D-143 and chapter 10 section 4.1a.</remarks>
internal sealed record OrganizationPolicyView(
    PolicyFieldView RequiredAssurance,
    PolicyFieldView LoginFactors,
    IReadOnlyDictionary<string, PolicyFieldView> Gates,
    PolicyFieldView CredentialRedundancy,
    PolicyFieldView SelfServiceRecovery,
    PolicyFieldView EmailDomains)
{
    /// <summary>
    /// The view of one organization's policy.
    /// </summary>
    /// <param name="policy">The policy and what of it is the organization's own.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The policy is absent.</exception>
    public static OrganizationPolicyView Of(OrganizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // The values are written in the one form the settings table and the
        // configuration route carry, so a policy reads the same wherever it is read.
        using var written = JsonDocument.Parse(Settings.PolicyDefault.Write(policy.Resolved));
        JsonElement root = written.RootElement;
        PolicyOverride own = policy.Overridden;
        Dictionary<string, PolicyFieldView> gates = new(StringComparer.Ordinal);

        foreach (JsonProperty gate in root.GetProperty("gates").EnumerateObject())
        {
            bool overridden = SettingText.TryRead(gate.Name, out StepUpAction action)
                && own.Gates is { } stated
                && stated.ContainsKey(action);

            gates[gate.Name] = new PolicyFieldView(gate.Value.Clone(), overridden);
        }

        return new OrganizationPolicyView(
            Field(root, "requiredAssurance", own.RequiredAssurance is not null),
            Field(root, "loginFactors", own.LoginFactors is not null),
            gates,
            Field(root, "credentialRedundancy", own.CredentialRedundancy is not null),
            Field(root, "selfServiceRecovery", own.SelfServiceRecovery is not null),
            Field(root, "emailDomains", own.EmailDomains is not null));
    }

    private static PolicyFieldView Field(JsonElement root, string name, bool overridden) =>
        new(root.GetProperty(name).Clone(), overridden);
}
