using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The address a deployment is prepared for, under which the host's sign-on client
/// registers its return address (LIB-TEST-003, AUTH-OIDC-006, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class TestDeploymentTests : IDisposable
{
    private const string Connection = "Host=database.example.test;Database=identity";

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// LIB-TEST-003, AUTH-OIDC-006: an address that is neither <c>https</c> nor
    /// <c>http</c> on a loopback IP literal, or that carries a query or a fragment, is
    /// refused before any command runs, since no return address is registered under it.
    /// </summary>
    /// <param name="address">The address the test gives.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("http://sample.example.test/identity")]
    [InlineData("http://localhost/identity")]
    [InlineData("http://192.0.2.10/identity")]
    [InlineData("ftp://sample.example.test/identity")]
    [InlineData("https://sample.example.test/identity?mounted=true")]
    [InlineData("https://sample.example.test/identity#mounted")]
    public async Task LIB_TEST_003_AnAddressNoReturnAddressIsRegisteredUnderIsRefusedAsync(string address)
    {
        var secrets = new FixedSecretSource(_randomness);

        Exception? refused = await Record.ExceptionAsync(() => TestDeployment
            .PrepareAsync(
                new Uri(address),
                new SignOnClient("sample-application"),
                secrets,
                Connection,
                Connection,
                [],
                TestContext.Current.CancellationToken)
            .AsTask());

        Assert.Equal("address", Assert.IsType<ArgumentException>(refused).ParamName);
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();
}
