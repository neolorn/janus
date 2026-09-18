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
