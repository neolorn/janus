using Microsoft.CodeAnalysis;

namespace Janus.Analyzers;

/// <summary>
/// The five rules of CONV-CODE-008, on which the gates of CONV-GATE-001 rely.
/// </summary>
internal static class Rules
{
    /// <summary>
    /// JAN0001, serving CONV-ERR-002: a catch block never yields a permitted,
    /// authenticated or successful outcome.
    /// </summary>
    internal static readonly DiagnosticDescriptor PermittedOutcomeFromCatch = new(
        id: "JAN0001",
        title: "A catch block returns a permitted, authenticated or successful outcome",
        messageFormat: "This catch block returns a successful outcome; a failure path never produces one (CONV-ERR-002)",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-ERR-002 is the enforcement behind every fail-closed requirement. A catch block either handles the fault meaningfully and yields a failure, or rethrows.");

    /// <summary>
    /// JAN0002, serving CONV-LOG-003: a value the specification forbids in logs never
    /// reaches a logging call.
    /// </summary>
    internal static readonly DiagnosticDescriptor NeverLoggedValue = new(
        id: "JAN0002",
        title: "A never-logged value reaches a logging call",
        messageFormat: "'{0}' is marked as never logged and cannot be passed to a logging call (CONV-LOG-003)",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-LOG-003 forbids credentials, secrets, health-implying data and compliance text from reaching any log. The types and members that carry them are marked with NeverLoggedAttribute.");

    /// <summary>
    /// JAN0003, serving CONV-CODE-001: every non-abstract class is sealed.
    /// </summary>
    internal static readonly DiagnosticDescriptor UnsealedType = new(
        id: "JAN0003",
        title: "A non-abstract class is not sealed",
        messageFormat: "'{0}' is not sealed; every type is sealed unless it is designed for inheritance (CONV-CODE-001)",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-CODE-001 seals every type that is not designed for inheritance, so an extension point is a declaration rather than an accident.");

    /// <summary>
    /// JAN0004, serving CONV-CODE-002: asynchronous code neither blocks on a task nor
    /// drops the cancellation token.
    /// </summary>
    internal static readonly DiagnosticDescriptor BlockingOnTask = new(
        id: "JAN0004",
        title: "A task is blocked on, or an asynchronous method takes no cancellation token",
        messageFormat: "{0} (CONV-CODE-002)",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-CODE-002 requires every asynchronous method to take a cancellation token as its last parameter and to pass it through, and forbids blocking on a task.");

    /// <summary>
    /// JAN0005, serving CONV-DESIGN-005: the outcome carried by a result is never
    /// discarded.
    /// </summary>
    internal static readonly DiagnosticDescriptor DiscardedResult = new(
        id: "JAN0005",
        title: "The value of a result is discarded",
        messageFormat: "The result of this expression is discarded; a failure cannot be ignored (CONV-DESIGN-005)",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-DESIGN-005 makes Result the carrier of every expected outcome. Discarding one discards the failure it may hold.");

    /// <summary>
    /// JAN0006, serving CONV-ERR-003: an exception is never swallowed.
    /// </summary>
    internal static readonly DiagnosticDescriptor SwallowedException = new(
        id: "JAN0006",
        title: "A catch block swallows the exception",
        messageFormat: "This catch block neither throws, rethrows, returns a failure result nor logs (CONV-ERR-003)",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "CONV-ERR-003 requires a caught exception to be handled meaningfully or rethrown, and forbids an empty catch block.");
}
