using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Storage.Tests;

/// <summary>
/// Subject identifiers for tests that need a row of their own. The tests of this
/// project share one database, so every one of them draws its own subject.
/// </summary>
internal static class Subjects
{
    /// <summary>
    /// Issues an identifier no other test holds.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static SubjectId New()
    {
        using var randomness = RandomNumberGenerator.Create();

        return SubjectId.New(randomness);
    }
}
