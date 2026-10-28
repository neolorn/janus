using System;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>PUT /records/collection/age</c>.
/// </summary>
/// <param name="DateOfBirth">The date entered.</param>
/// <remarks>Implements REC-AGE-001, chapter 09 section 7a.</remarks>
internal sealed record CollectionAgeRequest(DateOnly? DateOfBirth);
