using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The conditions of the alert table: what each one weighs, what it deduplicates
/// under, and how one is raised.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, OPS-ALERT-002 and chapter 10 section 5.23. The severity
/// of a condition is the table's, not the caller's: two places raising one condition
/// cannot disagree about how much it matters.
/// </remarks>
internal static class Alerts
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Nothing =
        new Dictionary<string, JsonElement>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// How much one condition matters, which decides whether SMS carries it as well
    /// as email (OPS-ALERT-003).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <returns>Its severity.</returns>
    public static AlertSeverity Severity(AlertCondition condition) =>
        condition switch
        {
            AlertCondition.AuthFailuresSustained => AlertSeverity.High,
            AlertCondition.RecoveryClustering => AlertSeverity.High,
            AlertCondition.ApproverVolume => AlertSeverity.High,
            AlertCondition.ReadVolumeAnomaly => AlertSeverity.High,
            AlertCondition.BreakGlassUsed => AlertSeverity.High,
            AlertCondition.BreakGlassGenerated => AlertSeverity.High,
            AlertCondition.ProtectedSettingChanged => AlertSeverity.High,
            AlertCondition.AlertDestinationChanged => AlertSeverity.High,
            AlertCondition.StepUpPolicyWeakened => AlertSeverity.High,
            AlertCondition.ConcurrentSessionsImplausible => AlertSeverity.High,
            AlertCondition.ErasureDeliveryExhausted => AlertSeverity.High,
            AlertCondition.CertificateRenewalFailed => AlertSeverity.High,
            AlertCondition.PrivacyDeadlineReached => AlertSeverity.High,
            AlertCondition.NoEmergencyCredential => AlertSeverity.High,
            AlertCondition.RestoreTestFailed => AlertSeverity.High,
            _ => AlertSeverity.Normal,
        };

    /// <summary>
    /// What one raised condition deduplicates under: the condition identifier, the scope
    /// it is raised under where it has one, and the account or actor the row names, where
    /// it names one (OPS-ALERT-002, D-177).
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="scope">The scope of chapter 10 section 5.23, or nothing where it has none.</param>
    /// <param name="named">Whose, or nothing where the row names no one.</param>
    /// <returns>The deduplication key.</returns>
    public static string Key(AlertCondition condition, string? scope, string? named)
    {
        string key = scope is null ? Named(condition) : Named(condition) + ":" + scope;

        return named is null ? key : key + ":" + named;
    }

    /// <summary>
    /// The deduplication key one raised alert carries, taken back out of its
    /// idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">What the alert carries.</param>
    /// <returns>The condition and the scope, without the instant.</returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public static string Deduplication(string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);

        int instant = idempotencyKey.LastIndexOf('@');

        return instant < 0 ? idempotencyKey : idempotencyKey[..instant];
    }

    /// <summary>
    /// Raises one condition.
    /// </summary>
    /// <param name="condition">What fired.</param>
    /// <param name="named">Whose, or nothing where the row names no one.</param>
    /// <param name="at">When.</param>
    /// <param name="details">The structured detail of the row.</param>
    /// <returns>The event to publish.</returns>
    public static AlertRaised Of(
        AlertCondition condition,
        string? named,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement>? details = null) =>
        Raised(condition, scope: null, named, at, details);

    /// <summary>
    /// Raises one condition under a scope of chapter 10 section 5.23, which the event
    /// carries and the deduplication key includes, so an alert under one scope never
    /// hides one under another (OPS-ALERT-002 AC3, D-177).
    /// </summary>
    /// <param name="condition">What fired.</param>
    /// <param name="scope">The scope, naming the one of several things it fired for.</param>
    /// <param name="at">When.</param>
    /// <param name="details">The structured detail of the row.</param>
    /// <returns>The event to publish.</returns>
    /// <exception cref="ArgumentNullException">The scope is absent.</exception>
    public static AlertRaised Scoped(
        AlertCondition condition,
        string scope,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement>? details = null)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Raised(condition, scope, named: null, at, details);
    }

    private static AlertRaised Raised(
        AlertCondition condition,
        string? scope,
        string? named,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement>? details) =>
        new(
            at,
            Key(condition, scope, named) + "@" + at.ToString("O", CultureInfo.InvariantCulture),
            condition,
            Severity(condition),
            details ?? Nothing)
        {
            Scope = scope,
        };

    // The identifier of chapter 10 section 5.23, which the enumeration carries as
    // the written name of each member.
    private static string Named(AlertCondition condition) => WrittenName.Of(condition);
}
