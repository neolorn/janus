using System;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What the authenticator app is given.
/// </summary>
/// <param name="Id">Which credential, to confirm against.</param>
/// <param name="Secret">The shared secret in Base32, shown as text.</param>
/// <param name="Uri">The <c>otpauth</c> address, shown as a QR code.</param>
/// <remarks>Implements AUTH-FACT-005, AUTH-FACT-007 and chapter 18 FE-PM-006.</remarks>
internal sealed record GeneratorEnrolmentView(Guid Id, string Secret, string Uri)
{
    /// <summary>
    /// Reads an enrolment.
    /// </summary>
    /// <param name="begun">The enrolment.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The enrolment is absent.</exception>
    public static GeneratorEnrolmentView Of(GeneratorEnrolment begun)
    {
        ArgumentNullException.ThrowIfNull(begun);

        return new GeneratorEnrolmentView(begun.Credential.Value, begun.Secret, begun.Address);
    }
}
