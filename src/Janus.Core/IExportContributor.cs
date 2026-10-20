using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a host registers to carry what it holds about a subject into the subject's
/// export, after the library's own sections.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003 and LIB-HOST-001 (D-203). A contributor is asked while the
/// export is assembled and before it is counted, so an export that would leave out what
/// a host holds is never returned and never spends the person's limit.
/// </remarks>
public interface IExportContributor
{
    /// <summary>
    /// What this contributor is called: under INT-SMS-003's name rule and distinct among
    /// contributors. Contributors are asked in the order of their names, and a failing
    /// one is named by it in the log.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Which of the host's declared resource types this contributor holds the records
    /// of. A type that declares an encrypted field or a sensitive category and that no
    /// registered contributor covers stops the deployment.
    /// </summary>
    IReadOnlyCollection<ResourceType> Covers { get; }

    /// <summary>
    /// Answers this contributor's sections of one subject's export.
    /// </summary>
    /// <param name="subject">Whose export it is.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>
    /// The sections, each under a name of INT-SMS-003's rule that no library section and
    /// no other contributor's takes, its values written as text under names of the same
    /// rule; or a failure, which fails the export: nothing is returned and nothing is
    /// counted.
    /// </returns>
    ValueTask<Result<IReadOnlyList<ExportSection>>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
