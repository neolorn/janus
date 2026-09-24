using System;

namespace Janus.Core;

/// <summary>
/// Marks an endpoint whose request and response bodies never reach a log, whatever
/// the deployment's logging is set to record.
/// </summary>
/// <remarks>
/// Implements BFF-LOG-002, CONV-LOG-003 and LIB-HOST-001. A host puts it on an endpoint
/// carrying data it declared sensitive, as an attribute or as endpoint metadata; the
/// library knows nothing of what the body holds. Every endpoint the library maps
/// carries it, since each carries a credential or a person's data.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class SensitiveBodyAttribute : Attribute;
