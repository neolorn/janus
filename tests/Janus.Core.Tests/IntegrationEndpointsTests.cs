using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The outbound addresses a deployment declares, and which of them would carry
/// their traffic in the clear (INT-GEN-001, LIB-EXT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class IntegrationEndpointsTests
{
    /// <summary>
    /// INT-GEN-001 AC2: a plaintext address is named with the integration it
    /// belongs to and the setting it was read from, which is what the refusal
    /// carries.
    /// </summary>
    [Fact]
    public void INT_GEN_001_AC2_APlaintextAddressIsNamedWithItsIntegrationAndKey()
    {
        var declared = IntegrationEndpoints.Of(
        [
            new IntegrationEndpoint("sms", "sms.gateway.baseurl", new Uri("https://sms.example.test")),
            new IntegrationEndpoint("shipping", "shipping.baseurl", new Uri("http://ship.example.test")),
        ]);

        IntegrationEndpoint insecure = Assert.Single(declared.Insecure);

        Assert.Equal("shipping", insecure.Integration);
        Assert.Equal("shipping.baseurl", insecure.Key);
    }

    /// <summary>
    /// INT-GEN-001 AC1: the scheme is the whole of the question, so an address that
    /// is not <c>https</c> is insecure whatever else it looks like.
    /// </summary>
    [Theory]
    [InlineData("https://sms.example.test", false)]
    [InlineData("https://sms.example.test:8443/send", false)]
    [InlineData("HTTPS://sms.example.test", false)]
    [InlineData("http://sms.example.test", true)]
    [InlineData("ftp://sms.example.test", true)]
    public void INT_GEN_001_AC1_AnAddressIsInsecureUnlessItIsHttps(string address, bool insecure)
    {
        var declared = IntegrationEndpoints.Of(
            [new IntegrationEndpoint("sms", "sms.gateway.baseurl", new Uri(address))]);

        Assert.Equal(insecure ? 1 : 0, declared.Insecure.Count);
    }

    /// <summary>
    /// A deployment that calls nothing out declares nothing, which is neither an
    /// error nor an insecure endpoint (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void None_ADeploymentThatDeclaresNothing_HasNothingInsecure()
    {
        Assert.Empty(IntegrationEndpoints.None.All);
        Assert.Empty(IntegrationEndpoints.None.Insecure);
    }

    /// <summary>
    /// An absent declaration is a mistake in the host's registration, refused where
    /// it is made (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void Of_AnAbsentDeclaration_IsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => IntegrationEndpoints.Of(null!));
        Assert.Throws<ArgumentNullException>(() => IntegrationEndpoints.Of([null!]));
    }
}
