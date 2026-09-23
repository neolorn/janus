using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Janus.Core.Configuration;

/// <summary>
/// The system policy: the one key of chapter 10 section 4.1 whose value is the policy
/// object of section 4.1a.
/// </summary>
/// <remarks>
/// Implements chapter 10 sections 4.1 and 4.1a, AUTH-PRIN-002 and REG-DOM-001. The
/// system policy locks no domain: <c>emailDomains</c> is written only through an
/// organization's domains, where each domain is verified for that organization.
/// </remarks>
public sealed class PolicySetting : Setting<Policy>
{
    internal PolicySetting(string key, SettingScope scope, Policy fallback)
        : base(key, scope, SettingDirection.AnyChange, required: false, fallback)
    {
    }

    /// <inheritdoc />
    public override Result<Policy> Accept(Policy value)
    {
        if (value is null)
        {
            return Result.Failure<Policy>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a policy"));
        }

        return value.EmailDomains.Count is 0
            ? Result.Success(value)
            : Result.Failure<Policy>(Refused(ErrorCodes.ConfigurationValueNotAllowed, "field", "emailDomains"));
    }

    /// <inheritdoc />
    private protected override Result<Policy> Parse(string stored) =>
        SettingText.Shape<Written>(stored, Malformed()).Match(Read, Result.Failure<Policy>);

    /// <inheritdoc />
    private protected override string Render(Policy value) =>
        SettingText.OfShape(
            new Written(
                SettingText.Of(value.RequiredAssurance),
                [.. value.LoginFactors.Select(factor => SettingText.Of(factor))],
                value.Gates.ToDictionary(
                    gate => SettingText.Of(gate.Key),
                    gate => new WrittenGate(
                        SettingText.Of(gate.Value.Level),
                        gate.Value.PhishingResistant,
                        XmlConvert.ToString(gate.Value.MaximumAge)),
                    StringComparer.Ordinal),
                SettingText.Of(value.CredentialRedundancy),
                value.SelfServiceRecovery,
                [.. value.EmailDomains]));

    private Result<Policy> Read(Written written)
    {
        if (written.RequiredAssurance is null
            || written.LoginFactors is null
            || written.Gates is null
            || written.CredentialRedundancy is null
            || written.EmailDomains is null
            || !SettingText.TryRead(written.RequiredAssurance, out AssuranceLevel assurance)
            || assurance is not (AssuranceLevel.Aal1 or AssuranceLevel.Aal2)
            || !SettingText.TryRead(written.CredentialRedundancy, out CredentialRedundancy redundancy))
        {
            return Result.Failure<Policy>(Malformed());
        }

        var factors = new HashSet<Factor>();

        foreach (string name in written.LoginFactors)
        {
            if (name is null || !SettingText.TryRead(name, out Factor factor) || !Policy.Admits(factor))
            {
                return Result.Failure<Policy>(Malformed());
            }

            factors.Add(factor);
        }

        var gates = new Dictionary<StepUpAction, Gate>();

        foreach (KeyValuePair<string, WrittenGate> entry in written.Gates)
        {
            if (entry.Value is null
                || entry.Value.Level is null
                || entry.Value.MaxAge is null
                || !SettingText.TryRead(entry.Key, out StepUpAction action)
                || !SettingText.TryRead(entry.Value.Level, out GateLevel level)
                || !Duration.TryParse(entry.Value.MaxAge, out TimeSpan age))
            {
                return Result.Failure<Policy>(Malformed());
            }

            gates[action] = new Gate(level, entry.Value.PhishingResistant, age);
        }

        return Result.Success(
            new Policy(
                assurance,
                factors,
                gates,
                redundancy,
                written.SelfServiceRecovery,
                written.EmailDomains));
    }

    private Error Malformed() =>
        Refused(ErrorCodes.ConfigurationValueNotAllowed, "allowed", "a policy");

    // The shape chapter 10 section 4.1a gives the policy object, which is what the
    // settings table holds and what the management application reads back.
    private sealed record Written(
        string? RequiredAssurance,
        string[]? LoginFactors,
        Dictionary<string, WrittenGate>? Gates,
        string? CredentialRedundancy,
        bool SelfServiceRecovery,
        string[]? EmailDomains);

    private sealed record WrittenGate(string? Level, bool PhishingResistant, string? MaxAge);
}
