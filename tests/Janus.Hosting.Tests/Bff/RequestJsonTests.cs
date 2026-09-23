using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A request body is read by the generated contexts and by nothing else, so a body no
/// context declares is a failure found here rather than a type reflected over at run
/// time (CONV-DESIGN-006, CONV-CODE-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class RequestJsonTests
{
    /// <summary>
    /// CONV-DESIGN-006: the options a request is read with hold the generated contexts
    /// and no reflection resolver.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_ARequestIsReadThroughTheGeneratedContextsOnly()
    {
        JsonOptions options = Configured();

        Assert.NotEmpty(options.SerializerOptions.TypeInfoResolverChain);
        Assert.All(
            options.SerializerOptions.TypeInfoResolverChain,
            resolver => Assert.IsAssignableFrom<JsonSerializerContext>(resolver));
    }

    /// <summary>
    /// CONV-DESIGN-006: every request type of the library is declared in one of those
    /// contexts, so it is read without reflection.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_EveryRequestBodyIsDeclaredInAContext()
    {
        JsonOptions options = Configured();

        Type[] bodies =
        [
            .. typeof(HostingRegistration).Assembly
                .GetTypes()
                .Where(type => type.Name.EndsWith("Body", StringComparison.Ordinal)
                    || type.Name.EndsWith("Request", StringComparison.Ordinal))
                .Where(Record),
        ];

        Assert.NotEmpty(bodies);
        Assert.All(
            bodies,
            body => Assert.True(
                options.SerializerOptions.TryGetTypeInfo(body, out _),
                body.Name + " is declared in no context."));
    }

    private static JsonOptions Configured()
    {
        var options = new JsonOptions();

        HostingRegistration.ReadThroughContexts(options);

        return options;
    }

    // A record is what the library reads a request into; a class named for a request,
    // such as the refusal of a malformed one, is not a body.
    private static bool Record(Type type) =>
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;
}
