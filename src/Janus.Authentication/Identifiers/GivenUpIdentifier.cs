using System;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// A value an account gave up, by a removal or a replace, as the undo reads it.
/// </summary>
/// <param name="Id">The identifier the value came from.</param>
/// <param name="Subject">Whose it was.</param>
/// <param name="Kind">Which of the three kinds it is.</param>
/// <param name="Entered">The form the person entered.</param>
/// <param name="Canonical">The form it is compared under.</param>
/// <param name="ExpiresAt">When the undo stops working.</param>
/// <remarks>Implements REG-IDENT-006 and CONV-LAYOUT-001.</remarks>
internal sealed record GivenUpIdentifier(
    IdentifierId Id,
    SubjectId Subject,
    IdentifierKind Kind,
    string Entered,
    string Canonical,
    DateTimeOffset ExpiresAt);
