using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// A startup fault names the check that stopped the start, so a host that logs the
/// exception alone has it (CONV-ERR-001, D-224).
/// </summary>
[Trait("kind", "unit")]
public sealed class StartupExceptionTests
{
    /// <summary>
    /// CONV-ERR-001 AC4: the message of a fault carrying a failure holds the operator's
    /// sentence, the failure's code and every detail's name and value.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AC4_TheMessageHoldsTheCodeAndEveryDetailsNameAndValue()
    {
        var failure = new Error(
            ErrorCodes.StartupSecretUnavailable,
            new Dictionary<string, JsonElement>
            {
                ["version"] = JsonSerializer.SerializeToElement(3),
                ["key"] = JsonSerializer.SerializeToElement("keyEncryptionKeys"),
            });

        var fault = new StartupException("The key-encryption key cannot be read.", failure);

        Assert.Equal(
            "The key-encryption key cannot be read. (model.startup.secretunavailable; key \"keyEncryptionKeys\", version 3)",
            fault.Message);
        Assert.Same(failure, fault.Failure);
    }

    /// <summary>
    /// CONV-ERR-001 AC4: a failure with no details adds its code alone.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AC4_AFailureWithNoDetailsAddsItsCodeAlone()
    {
        var fault = new StartupException(
            "No secret source is declared.",
            Error.From(ErrorCodes.StartupDeclarationMissing));

        Assert.Equal("No secret source is declared. (model.startup.declarationmissing)", fault.Message);
    }

    /// <summary>
    /// A fault carrying no failure keeps the operator's sentence as it was written.
    /// </summary>
    [Fact]
    public void CONV_ERR_001_AFaultWithoutAFailureKeepsItsSentence()
    {
        var fault = new StartupException("The model declares no resource type.");

        Assert.Equal("The model declares no resource type.", fault.Message);
        Assert.Null(fault.Failure);
    }
}
