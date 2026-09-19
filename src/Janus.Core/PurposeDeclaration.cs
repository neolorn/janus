namespace Janus.Core;

/// <summary>
/// One purpose a resource type is processed for, and the lawful basis it rests on.
/// </summary>
/// <param name="Name">The purpose, as the records of processing name it.</param>
/// <param name="Basis">The key of the declared lawful basis it rests on.</param>
/// <param name="Assessment">
/// The reference to the legitimate interest assessment, where the basis requires one.
/// </param>
/// <remarks>Implements AUTHZ-MODEL-003, PRIV-BASIS-001, PRIV-BASIS-002.</remarks>
public sealed record PurposeDeclaration(string Name, string Basis, string? Assessment = null);
