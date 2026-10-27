using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Mvc;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A host's controller marked as answering anyone, reached only through a dynamic route
/// the host maps, whose action carries no mark of its own (BFF-HOST-002).
/// </summary>
[AllowNoSession]
public sealed class NoticeboardController : ControllerBase
{
    /// <summary>
    /// The text the board answers with, so a test can tell the action ran.
    /// </summary>
    public const string Boarded = "boarded";

    /// <summary>
    /// The board, which answers anyone, its controller being marked.
    /// </summary>
    /// <returns>The board.</returns>
    public IActionResult Board() => Ok(Boarded);
}
