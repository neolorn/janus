using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Janus.Hosting.Tests.Bff;
using Xunit;

namespace Janus.Hosting.Tests;

/// <summary>
/// The packages the library's solution packs from the build these tests run beside, read
/// once for the tests that ask what each carries and declares (LIB-PKG-001,
/// CONV-LAYOUT-001).
/// </summary>
public sealed class PackedLibrary : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "packed-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Each package packed, by its identifier.
    /// </summary>
    public IReadOnlyDictionary<string, PackedPackage> Packages { get; private set; } =
        new Dictionary<string, PackedPackage>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        string configuration = typeof(PackedLibrary).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(metadata => metadata.Key == "Configuration")
            .Value!;

        var start = new ProcessStartInfo(Dotnet())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Repository.Root(),
        };

        foreach (string argument in new[]
        {
            "pack", "Janus.slnx", "--configuration", configuration, "--no-build", "--no-restore", "--output", _folder,
        })
        {
            start.ArgumentList.Add(argument);
        }

        using Process pack = Process.Start(start)
            ?? throw new InvalidOperationException("The pack did not start.");

        Task<string> output = pack.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = pack.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        await pack.WaitForExitAsync(TestContext.Current.CancellationToken);

        if (pack.ExitCode != 0)
        {
            throw new InvalidOperationException("The pack failed: " + await output + await error);
        }

        Packages = Directory
            .GetFiles(_folder, "*.nupkg")
            .Select(PackedPackage.Read)
            .ToDictionary(package => package.Identifier, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    // The host the tests run under where the platform names it, and the one on the path
    // otherwise.
    private static string Dotnet() =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } named ? named : "dotnet";
}
