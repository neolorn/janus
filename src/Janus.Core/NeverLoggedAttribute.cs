using System;

namespace Janus.Core;

/// <summary>
/// Marks a type or member whose value never reaches a log: credentials and secrets,
/// sensitive host data, and the content of compliance text.
/// </summary>
/// <remarks>
/// Implements CONV-LOG-003. The marker is what rule JAN0002 of CONV-CODE-008 reads, so
/// a logging call that takes a marked value fails the build rather than a review.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Parameter,
    Inherited = false)]
public sealed class NeverLoggedAttribute : Attribute;
