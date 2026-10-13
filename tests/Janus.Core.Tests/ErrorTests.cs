using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The failure an expected outcome carries (CONV-DESIGN-005, LIB-API-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErrorTests
{
    /// <summary>
    /// A failure built from a code alone carries the code and no context.
    /// </summary>
    [Fact]
    public void From_CodeAlone_CarriesTheCodeAndNoDetails()
    {
        var failure = Error.From(ErrorCodes.ConfigurationKeyProtected);

        Assert.Equal(ErrorCodes.ConfigurationKeyProtected, failure.Code);
        Assert.Empty(failure.Details);
    }

    /// <summary>
    /// A failure built from a named value carries that value under its name.
    /// </summary>
    [Fact]
    public void From_NamedValue_CarriesTheValueUnderItsName()
    {
        var failure = Error.From(
            ErrorCodes.ConfigurationValueBelowFloor,
            "floor",
            JsonSerializer.SerializeToElement(8));

        Assert.Equal(8, failure.Details["floor"].GetInt32());
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC2: the throttled refusal says when the next attempt is looked
    /// at, as the one <c>retryAt</c> every throttle of the library answers with, and
    /// nothing else.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_002_AC2_TheThrottledRefusalCarriesTheInstantAndNothingElse()
    {
        var lifts = new DateTimeOffset(2026, 3, 1, 12, 0, 4, 200, TimeSpan.Zero);

        var refusal = Error.Throttled(lifts);

        Assert.Equal(ErrorCodes.Throttled, refusal.Code);
        Assert.Equal("retryAt", Assert.Single(refusal.Details).Key);
        Assert.Equal(lifts, Assert.Single(refusal.Details).Value.GetDateTimeOffset());
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC2: the instant is written in UTC whatever offset it was given in.
    /// </summary>
    [Fact]
    public void Throttled_AnInstantWithAnOffset_IsWrittenInUtc()
    {
        var lifts = new DateTimeOffset(2026, 3, 1, 15, 0, 0, TimeSpan.FromHours(3));

        var refusal = Error.Throttled(lifts);

        Assert.Equal("2026-03-01T12:00:00+00:00", refusal.Details["retryAt"].GetString());
    }

    /// <summary>
    /// A failure without a code is a fault, not a failure: the catalogue is the only
    /// source of a code.
    /// </summary>
    [Fact]
    public void Constructor_UnsetCode_Refused() =>
        Assert.Throws<ArgumentException>(() => new Error(default, new Dictionary<string, JsonElement>()));

    /// <summary>
    /// Absent details are refused; a failure with no context carries an empty set.
    /// </summary>
    [Fact]
    public void Constructor_AbsentDetails_Refused() =>
        Assert.Throws<ArgumentNullException>(() => new Error(ErrorCodes.ConfigurationKeyProtected, null!));

    /// <summary>
    /// LIB-API-003 AC1: nothing crossing the boundary is a sentence a person reads, so
    /// the failure exposes no prose member beyond the code and its structured details.
    /// </summary>
    [Fact]
    public void LIB_API_003_AC1_TheFailureCarriesNoProse()
    {
        PropertyInfo[] prose = Array.FindAll(
            typeof(Error).GetProperties(),
            property => property.PropertyType == typeof(string));

        Assert.Empty(prose);
    }
}
