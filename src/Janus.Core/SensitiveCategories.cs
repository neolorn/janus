using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The default sensitive-data category declaration, shipped so a deployment in the
/// jurisdiction it belongs to declares it rather than writes it.
/// </summary>
/// <remarks>
/// Implements PRIV-SENS-001 and chapter 10 section 5.9. The categories are labels the
/// records of processing report under and nothing else; a deployment in another
/// jurisdiction declares its own list and changes no code. One of them,
/// <c>children</c>, is read by name, and by the register alone (PRIV-ROPA-001).
/// </remarks>
public static class SensitiveCategories
{
    /// <summary>
    /// What a resource type declared sensitive under the children's category names,
    /// which is the one category the library reads by name.
    /// </summary>
    public const string Children = "children";

    /// <summary>
    /// The eight chapter 10 section 5.9 gives, in its order.
    /// </summary>
    public static IReadOnlyList<string> Default { get; } =
    [
        "health",
        "genetic",
        "biometric",
        "financial",
        "religious-belief",
        "political-view",
        "criminal-record",
        Children,
    ];
}
