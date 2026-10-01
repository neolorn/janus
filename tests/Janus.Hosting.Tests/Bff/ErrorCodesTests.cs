using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Janus.Core;
using Janus.Hosting.Bff;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The codes as a contract, each with the status it answers with (LIB-API-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class ErrorCodesTests
{
    /// <summary>
    /// LIB-API-001 AC2: each code and the status it answers with are the contract, so a
    /// code added or dropped, or answered with another status, fails here and carries
    /// its version bump.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheStatusesAreTheContract()
    {
        string[] declared =
        [
            .. typeof(ErrorCodes)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(ErrorCode))
                .Select(property => (ErrorCode)property.GetValue(obj: null)!)
                .Select(code => code + " " + ApiStatus.Of(code).ToString(CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(
            File.ReadAllText(Path.Combine(Repository.Root(), "tests", "Janus.Hosting.Tests", "error-statuses.txt"))
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries),
            declared);
    }
}
