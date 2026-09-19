using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Janus.Core.Configuration;

/// <summary>
/// The written forms the families of chapter 10 section 4 hold their values in.
/// </summary>
/// <remarks>Implements OPS-CFG-008, D-151.</remarks>
internal static class SettingForms
{
    /// <summary>
    /// A length of time, written in ISO 8601.
    /// </summary>
    public static SettingForm<TimeSpan> Duration { get; } =
        new(ReadDuration, XmlConvert.ToString, "an ISO 8601 duration");

    /// <summary>
    /// A value that is on or off.
    /// </summary>
    public static SettingForm<bool> Flag { get; } = new(ReadFlag, WriteFlag, "true or false");

    /// <summary>
    /// What an organization changes about the system policy, written as chapter 10
    /// section 4.1a writes the policy object with every field optional.
    /// </summary>
    public static SettingForm<PolicyOverride> Override { get; } =
        new(ReadOverride, WriteOverride, "a policy override");

    private static Result<TimeSpan> ReadDuration(string stored, Error malformed) =>
        Configuration.Duration.TryParse(stored, out TimeSpan duration)
            ? Result.Success(duration)
            : Result.Failure<TimeSpan>(malformed);

    private static Result<bool> ReadFlag(string stored, Error malformed) => stored switch
    {
        "true" => Result.Success(true),
        "false" => Result.Success(false),
        _ => Result.Failure<bool>(malformed),
    };

    private static string WriteFlag(bool value) => value ? "true" : "false";

    private static Result<PolicyOverride> ReadOverride(string stored, Error malformed) =>
        SettingText
            .Shape<Written>(stored, malformed)
            .Match(written => Read(written, malformed), Result.Failure<PolicyOverride>);

    private static Result<PolicyOverride> Read(Written written, Error malformed)
    {
        AssuranceLevel? assurance = null;

        if (written.RequiredAssurance is { } floor)
        {
            if (!SettingText.TryRead(floor, out AssuranceLevel read)
                || read is not (AssuranceLevel.Aal1 or AssuranceLevel.Aal2))
            {
                return Result.Failure<PolicyOverride>(malformed);
            }

            assurance = read;
        }

        CredentialRedundancy? redundancy = null;

        if (written.CredentialRedundancy is { } rule)
        {
            if (!SettingText.TryRead(rule, out CredentialRedundancy read))
            {
                return Result.Failure<PolicyOverride>(malformed);
            }

            redundancy = read;
        }

        HashSet<Factor>? factors = null;

        if (written.LoginFactors is { } names)
        {
            factors = [];

            foreach (string name in names)
            {
                if (name is null || !SettingText.TryRead(name, out Factor factor))
                {
                    return Result.Failure<PolicyOverride>(malformed);
                }

                factors.Add(factor);
            }
        }

        Dictionary<StepUpAction, Gate>? gates = null;

        if (written.Gates is { } bound)
        {
            gates = [];

            foreach (KeyValuePair<string, WrittenGate> entry in bound)
            {
                if (entry.Value is null
                    || entry.Value.Level is null
                    || entry.Value.MaximumAge is null
                    || !SettingText.TryRead(entry.Key, out StepUpAction action)
                    || !SettingText.TryRead(entry.Value.Level, out GateLevel level)
                    || !Configuration.Duration.TryParse(entry.Value.MaximumAge, out TimeSpan age))
                {
                    return Result.Failure<PolicyOverride>(malformed);
                }

                gates[action] = new Gate(level, entry.Value.PhishingResistant, age);
            }
        }

        return Result.Success(
            new PolicyOverride(
                assurance,
                factors,
                gates,
                redundancy,
                written.SelfServiceRecovery,
                written.EmailDomains));
    }

    private static string WriteOverride(PolicyOverride value) =>
        SettingText.OfShape(
            new Written(
                value.RequiredAssurance is { } assurance ? SettingText.Of(assurance) : null,
                value.LoginFactors is { } factors
                    ? [.. factors.Select(factor => SettingText.Of(factor))]
                    : null,
                value.Gates?.ToDictionary(
                    gate => SettingText.Of(gate.Key),
                    gate => new WrittenGate(
                        SettingText.Of(gate.Value.Level),
                        gate.Value.PhishingResistant,
                        XmlConvert.ToString(gate.Value.MaximumAge)),
                    StringComparer.Ordinal),
                value.CredentialRedundancy is { } redundancy ? SettingText.Of(redundancy) : null,
                value.SelfServiceRecovery,
                value.EmailDomains is { } domains ? [.. domains] : null));

    private sealed record Written(
        string? RequiredAssurance,
        string[]? LoginFactors,
        Dictionary<string, WrittenGate>? Gates,
        string? CredentialRedundancy,
        bool? SelfServiceRecovery,
        string[]? EmailDomains);

    private sealed record WrittenGate(string? Level, bool PhishingResistant, string? MaximumAge);
}
