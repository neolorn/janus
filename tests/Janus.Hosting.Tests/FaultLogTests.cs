using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;
using Janus.Storage;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// What the log keeps of a fault (BFF-ERR-002, CONV-LOG-003, OPS-SEC-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class FaultLogTests
{
    /// <summary>
    /// OPS-SEC-003 AC3 (D-183): a fault that carries a code is kept in the log with that
    /// code and its details, the key and the version among them, beside its type, and an
    /// inner one is kept the same way.
    /// </summary>
    [Fact]
    public void OPS_SEC_003_AC3_AFaultThatCarriesACodeIsLoggedWithItsCodeAndDetails()
    {
        var unheld = new CodedFault(new Error(
            ErrorCodes.StartupSecretUnavailable,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["version"] = JsonSerializer.SerializeToElement(3),
                ["key"] = JsonSerializer.SerializeToElement("keyEncryptionKeys"),
            }));

        string named = typeof(CodedFault).FullName
            + " model.startup.secretunavailable key=\"keyEncryptionKeys\" version=3";

        Assert.Equal(named, FaultLog.Of(unheld));
        Assert.EndsWith(
            "inner " + named,
            FaultLog.Of(new InvalidOperationException("a read failed", unheld)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// BFF-ERR-002 AC2, CONV-LOG-003: a fault that carries no code is kept by its type
    /// alone, and never by its message.
    /// </summary>
    [Fact]
    public void BFF_ERR_002_AC2_AFaultThatCarriesNoCodeIsLoggedByItsTypeAlone() =>
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            FaultLog.Of(new InvalidOperationException("a value a person wrote")));
}
