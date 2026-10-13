using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// The code generator a registration session has begun and not confirmed, as it is
/// written into the session's encrypted column.
/// </summary>
/// <param name="Id">The identifier the credential carries once it is confirmed.</param>
/// <param name="Label">What the person called it.</param>
/// <param name="Secret">The shared secret.</param>
/// <remarks>Implements REG-SESS-001, AUTH-FACT-006 and AUTH-FACT-007.</remarks>
[NeverLogged]
internal sealed record StagedGeneratorDocument(Guid Id, string Label, byte[] Secret);
