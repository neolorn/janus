using System;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// An enrolment just begun: the credential it created and the secret to show once,
/// as a QR code and as text to type.
/// </summary>
/// <param name="Id">Which credential.</param>
/// <param name="Secret">
/// The shared secret. It leaves the library once, at enrolment, and is never read
/// back.
/// </param>
/// <remarks>Implements AUTH-FACT-007.</remarks>
[NeverLogged]
internal sealed record TotpEnrolment(AuthenticatorId Id, ReadOnlyMemory<byte> Secret);
