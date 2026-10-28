using System;
using System.Collections.Generic;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// Everything a collection session staged, as it is written into its one encrypted
/// column.
/// </summary>
/// <param name="AsksAge">Whether the session asks the age question.</param>
/// <param name="Language">The language a code goes out in.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="AdultAffirmed">The affirmation derived from the date.</param>
/// <param name="Group">The age group recorded instead, where one was.</param>
/// <param name="DateOfBirth">The date retained, where the deployment retains it.</param>
/// <param name="AnsweredAgeAt">When the age question was answered.</param>
/// <param name="AgeRefused">Whether an under-age date ended it.</param>
/// <param name="Contacts">Every contact staged.</param>
/// <remarks>Implements REC-COL-001 and REC-AGE-001.</remarks>
internal sealed record CollectionSessionDocument(
    bool AsksAge,
    string Language,
    DateTimeOffset CreatedAt,
    bool? AdultAffirmed,
    string? Group,
    DateOnly? DateOfBirth,
    DateTimeOffset? AnsweredAgeAt,
    bool AgeRefused,
    IReadOnlyList<CollectedContactDocument> Contacts);
