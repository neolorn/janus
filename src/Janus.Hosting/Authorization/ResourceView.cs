using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One record, named by its type and its identifier.
/// </summary>
/// <param name="ResourceType">The kind of thing it is.</param>
/// <param name="ResourceId">Which one.</param>
/// <remarks>Implements AUTHZ-GATE-004 in the shape D-153 fixes.</remarks>
internal sealed record ResourceView(string ResourceType, string ResourceId)
{
    /// <summary>
    /// The view of a record.
    /// </summary>
    /// <param name="resource">The record.</param>
    /// <returns>The view.</returns>
    public static ResourceView Of(ResourceReference resource) =>
        new(resource.Type.ToString(), resource.Id.ToString());
}
