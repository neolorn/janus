using System;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Authorization.Tests;

/// <summary>
/// Identifiers for tests that need one nothing else holds.
/// </summary>
internal static class Identifiers
{
    /// <summary>
    /// Issues an account identifier.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static SubjectId Subject()
    {
        using var randomness = RandomNumberGenerator.Create();

        return SubjectId.New(randomness);
    }

    /// <summary>
    /// Issues an organization identifier.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static OrganizationId Organization() => OrganizationId.New(TimeProvider.System);

    /// <summary>
    /// Issues a group identifier.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static GroupId Group() => GroupId.New(TimeProvider.System);

    /// <summary>
    /// Issues a grant identifier.
    /// </summary>
    /// <returns>The identifier.</returns>
    internal static GrantId Grant() => GrantId.New(TimeProvider.System);

    /// <summary>
    /// Issues an identifier for one of the host's records.
    /// </summary>
    /// <param name="type">The kind of record.</param>
    /// <returns>The reference.</returns>
    internal static ResourceReference Resource(string type) =>
        new(ResourceType.Parse(type), ResourceId.Parse(Guid.NewGuid().ToString()));
}
