using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Mvc;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A host's controller, reached only through a dynamic route the host maps, with one
/// action left unmarked and one marked as answering anyone (BFF-HOST-002).
/// </summary>
public sealed class ArchiveController : ControllerBase
{
    /// <summary>
    /// The text the shelf answers with, so a test can tell the action ran.
    /// </summary>
    public const string Shelved = "shelved";

    /// <summary>
    /// The text the notice answers with, so a test can tell the action ran.
    /// </summary>
    public const string Noticed = "noticed";

    /// <summary>
    /// The archive's shelf, which answers only a signed-in person, being unmarked.
    /// </summary>
    /// <returns>The shelf.</returns>
    public IActionResult Shelf() => Ok(Shelved);

    /// <summary>
    /// The archive's notice, which answers anyone, being marked.
    /// </summary>
    /// <returns>The notice.</returns>
    [AllowNoSession]
    public IActionResult Notice() => Ok(Noticed);
}
