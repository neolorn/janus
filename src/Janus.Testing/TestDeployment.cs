using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// An empty database prepared as a production one is prepared, through the installed
/// <c>Janus.Cli</c> tool: <c>migrate</c>, then <c>bootstrap</c>, then
/// <c>register-client</c> for the host's browser application; what <c>bootstrap</c>
/// printed is kept.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003, OPS-ENV-001, OPS-MIG-001, OPS-BOOT-001 and AUTH-OIDC-001.
/// The tool runs as the host's local tool, <c>dotnet tool run identity</c> on the .NET
/// host the path names, from the directory the test runs in, so the host's tool manifest
/// names the version it runs.
/// <c>migrate</c> reads a document holding the migration connection and nothing else;
/// the other commands read the key document of the host's <see cref="FixedSecretSource"/>.
/// A command that refuses ends the preparation with what it wrote on its standard error,
/// a code and its details and never a secret.
/// </remarks>
public sealed class TestDeployment
{
    private const string Tool = "identity";

    private const string SignOnReturn = "auth/signon/return";

    // The .NET host on the path, which runs the local tool the manifest names.
    private const string DotnetHost = "dotnet";

    private TestDeployment(Uri address, Uri enrolmentLink, OrganizationId administrativeOrganization)
    {
        Address = address;
        EnrolmentLink = enrolmentLink;
        AdministrativeOrganization = administrativeOrganization;
    }

    /// <summary>
    /// Where the test serves the host's application: its origin and the path the browser
    /// profile is mounted under.
    /// </summary>
    public Uri Address { get; }

    /// <summary>
    /// The enrolment link <c>bootstrap</c> printed, which enrols the first administrator.
    /// </summary>
    public Uri EnrolmentLink { get; }

    /// <summary>
    /// The administrative organization <c>bootstrap</c> created.
    /// </summary>
    public OrganizationId AdministrativeOrganization { get; }

    /// <summary>
    /// Prepares an empty database for the host's application.
    /// </summary>
    /// <param name="address">
    /// Where the test serves the host's application: an <c>https</c> origin, or an
    /// <c>http</c> one on a loopback IP literal, and the path the browser profile is
    /// mounted under.
    /// </param>
    /// <param name="client">The host's browser application, registered as a sign-on client.</param>
    /// <param name="secrets">The secret source the host's application reads.</param>
    /// <param name="migrationConnection">The connection <c>migrate</c> applies the schema under.</param>
    /// <param name="applicationConnection">The connection the application runs under.</param>
    /// <param name="bootstrap">The arguments <c>bootstrap</c> takes, which name the deployment.</param>
    /// <param name="cancellationToken">Abandons the preparation.</param>
    /// <returns>The prepared deployment.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The address is not one a return address can be registered under, or a connection is
    /// empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">A command refused, or printed what it does not print.</exception>
    public static async ValueTask<TestDeployment> PrepareAsync(
        Uri address,
        SignOnClient client,
        FixedSecretSource secrets,
        string migrationConnection,
        string applicationConnection,
        IReadOnlyList<string> bootstrap,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationConnection);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationConnection);
        ArgumentNullException.ThrowIfNull(bootstrap);

        Uri mounted = Served(address);
        byte[] keys = secrets.KeyDocument(applicationConnection);

        try
        {
            _ = await RunAsync(
                    ["migrate"],
                    Encoding.UTF8.GetBytes(new JsonObject { ["connection"] = migrationConnection }.ToJsonString()),
                    cancellationToken)
                .ConfigureAwait(false);

            string[] printed = (await RunAsync(["bootstrap", .. bootstrap], keys, cancellationToken).ConfigureAwait(false))
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (printed is not [{ } link, { } organization]
                || !Uri.TryCreate(link, UriKind.Absolute, out Uri? enrolment)
                || !OrganizationId.TryParse(organization, provider: null, out OrganizationId created))
            {
                throw new InvalidOperationException("bootstrap printed what it does not print.");
            }

            _ = await RunAsync(
                    [
                        "register-client",
                        "--client", client.ClientId,
                        "--name", client.ClientId,
                        "--kind", "browser-application",
                        "--redirect", new Uri(mounted, SignOnReturn).AbsoluteUri,
                        "--scopes", "openid",
                    ],
                    keys,
                    cancellationToken)
                .ConfigureAwait(false);

            return new TestDeployment(mounted, enrolment, created);
        }
        finally
        {
            Array.Clear(keys);
        }
    }

    /// <summary>
    /// The address the application is served at, where a return address can be
    /// registered under it (AUTH-OIDC-006).
    /// </summary>
    /// <param name="address">The address the test gives.</param>
    /// <returns>The address, ending in a separator.</returns>
    /// <exception cref="ArgumentNullException">The address is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The address is not absolute, or is neither <c>https</c> nor <c>http</c> on a
    /// loopback IP literal, or carries a query or a fragment.
    /// </exception>
    internal static Uri Served(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        bool admitted = address.IsAbsoluteUri
            && address.Query.Length is 0
            && address.Fragment.Length is 0
            && (address.Scheme == Uri.UriSchemeHttps
                || (address.Scheme == Uri.UriSchemeHttp
                    && IPAddress.TryParse(address.IdnHost, out IPAddress? literal)
                    && IPAddress.IsLoopback(literal)));

        return admitted
            ? TestSession.Mounted(address)
            : throw new ArgumentException(
                "The address is neither https nor http on a loopback IP literal, with no query or fragment.",
                nameof(address));
    }

    // One run of the tool, the document piped to its standard input.
    private static async Task<string> RunAsync(
        IReadOnlyList<string> arguments,
        byte[] document,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(DotnetHost)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("tool");
        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Tool);

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process run = Process.Start(start)
            ?? throw new InvalidOperationException("The tool did not start.");

        Task<string> output = run.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = run.StandardError.ReadToEndAsync(cancellationToken);

        await run.StandardInput.BaseStream.WriteAsync(document, cancellationToken).ConfigureAwait(false);
        run.StandardInput.Close();

        await run.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        string printed = await output.ConfigureAwait(false);
        string refused = await error.ConfigureAwait(false);

        return run.ExitCode is 0
            ? printed
            : throw new InvalidOperationException(arguments[0] + " was refused: " + refused.Trim());
    }
}
