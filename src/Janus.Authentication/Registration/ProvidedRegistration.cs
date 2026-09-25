using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// What a social provider's identity did at the email step of a registration.
/// </summary>
/// <param name="Linked">
/// Whether the identity is linked to an account already, which makes the attempt a
/// sign-in; the session is then left as it was, for the caller to end once the person
/// is signed in.
/// </param>
/// <param name="State">The registration's state, where the identity was staged on it.</param>
/// <remarks>Implements REG-IDENT-008.</remarks>
internal sealed record ProvidedRegistration(bool Linked, RegistrationState? State);
