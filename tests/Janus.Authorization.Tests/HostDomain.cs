using System;
using Janus.Core;

namespace Janus.Authorization.Tests;

/// <summary>
/// A host's own domain, as a host would declare it: a workspace owned by an
/// organization, folders nested in it, documents in the folders, and a reviewer named
/// on a folder whose role follows from that fact alone.
/// </summary>
/// <remarks>
/// CONV-TEST-005: the fixture has the structure permission bugs hide in, which a flat
/// set of unrelated entities would not. Nothing here is the library's: these are the
/// host's types, and the library never names one.
/// </remarks>
internal static class HostDomain
{
    /// <summary>
    /// The outermost container, which names the organization owning everything in it.
    /// </summary>
    internal sealed class Workspace
    {
        /// <summary>The workspace.</summary>
        public Guid Id { get; init; }

        /// <summary>The organization owning it.</summary>
        public Guid OrganizationId { get; init; }
    }

    /// <summary>
    /// A folder in a workspace, which may name the person reviewing what is in it.
    /// </summary>
    internal sealed class Folder
    {
        /// <summary>The folder.</summary>
        public Guid Id { get; init; }

        /// <summary>The workspace containing it.</summary>
        public Guid WorkspaceId { get; init; }

        /// <summary>The person reviewing what is in it, where one is named.</summary>
        public SubjectId Reviewer { get; init; }
    }

    /// <summary>
    /// A document in a folder, whose body is held under its author's key.
    /// </summary>
    internal sealed class Document
    {
        /// <summary>The document.</summary>
        public Guid Id { get; init; }

        /// <summary>The folder containing it.</summary>
        public Guid FolderId { get; init; }

        /// <summary>The body, held as ciphertext.</summary>
        public byte[] Body { get; init; } = [];

        /// <summary>The subject whose key the body is held under.</summary>
        public SubjectId Author { get; init; }
    }

    /// <summary>
    /// A document not yet published, kept in the same folder.
    /// </summary>
    internal sealed class Draft
    {
        /// <summary>The draft.</summary>
        public Guid Id { get; init; }

        /// <summary>The folder containing it.</summary>
        public Guid FolderId { get; init; }
    }

    /// <summary>
    /// A workspace of one organization.
    /// </summary>
    /// <param name="organization">The organization owning it.</param>
    /// <returns>The workspace.</returns>
    public static Workspace NewWorkspace(Guid organization) =>
        new() { Id = Guid.CreateVersion7(), OrganizationId = organization };

    /// <summary>
    /// A folder in a workspace.
    /// </summary>
    /// <param name="workspace">The workspace containing it.</param>
    /// <param name="reviewer">The person reviewing what is in it.</param>
    /// <returns>The folder.</returns>
    public static Folder NewFolder(Guid workspace, SubjectId reviewer) =>
        new() { Id = Guid.CreateVersion7(), WorkspaceId = workspace, Reviewer = reviewer };

    /// <summary>
    /// A document in a folder.
    /// </summary>
    /// <param name="folder">The folder containing it.</param>
    /// <param name="author">The subject whose key the body is held under.</param>
    /// <returns>The document.</returns>
    public static Document NewDocument(Guid folder, SubjectId author) =>
        new() { Id = Guid.CreateVersion7(), FolderId = folder, Author = author };

    /// <summary>
    /// A draft in a folder.
    /// </summary>
    /// <param name="folder">The folder containing it.</param>
    /// <returns>The draft.</returns>
    public static Draft NewDraft(Guid folder) =>
        new() { Id = Guid.CreateVersion7(), FolderId = folder };

    /// <summary>
    /// A declaration of the whole domain, valid as it stands.
    /// </summary>
    /// <returns>The builder, so a test may change one thing before building.</returns>
    public static AuthorizationDeclarationBuilder Declared() =>
        new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
            .SensitiveCategory("financial")
            .Permission("document:read")
            .Permission("document:edit")
            .Relationship<Folder>("reviewer", "folder", "folders", folder => folder.Reviewer)
            .Resource<Workspace>("workspace", workspace => workspace
                .BelongsToOrganization()
                .Purpose("collaboration", "contract"))
            .Resource<Folder>("folder", folder => folder
                .ContainedIn("workspace")
                .Purpose("collaboration", "contract")
                .Derivation("reviewer", "reader"))
            .Resource<Document>("document", document => document
                .ContainedIn("folder")
                .Purpose("collaboration", "contract")
                .Encrypted(item => item.Body, item => item.Author));
}
