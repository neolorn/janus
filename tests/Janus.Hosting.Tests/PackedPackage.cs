using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Janus.Hosting.Tests;

/// <summary>
/// One packed package: what it holds and what its manifest declares.
/// </summary>
/// <param name="Identifier">The package's identifier.</param>
/// <param name="Version">The version it was packed at.</param>
/// <param name="Entries">Every file it holds, by its path inside the package.</param>
/// <param name="Manifest">Its manifest.</param>
public sealed record PackedPackage(string Identifier, string Version, IReadOnlyList<string> Entries, XDocument Manifest)
{
    /// <summary>
    /// The packages it declares as dependencies, each with the version it asks.
    /// </summary>
    public IReadOnlyDictionary<string, string> Dependencies =>
        Manifest
            .Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .ToDictionary(
                dependency => dependency.Attribute("id")!.Value,
                dependency => dependency.Attribute("version")!.Value,
                StringComparer.Ordinal);

    /// <summary>
    /// The shared frameworks it references.
    /// </summary>
    public IReadOnlyList<string> FrameworkReferences =>
    [
        .. Manifest
            .Descendants()
            .Where(element => element.Name.LocalName == "frameworkReference")
            .Select(reference => reference.Attribute("name")!.Value),
    ];

    /// <summary>
    /// Reads a package file.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The package.</returns>
    public static PackedPackage Read(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);

        ZipArchiveEntry nuspec = archive.Entries.Single(entry =>
            !entry.FullName.Contains('/', StringComparison.Ordinal)
            && entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));

        XDocument manifest;

        using (Stream read = nuspec.Open())
        {
            manifest = XDocument.Load(read);
        }

        XElement metadata = manifest.Root!.Elements().Single(element => element.Name.LocalName == "metadata");

        return new PackedPackage(
            metadata.Elements().Single(element => element.Name.LocalName == "id").Value,
            metadata.Elements().Single(element => element.Name.LocalName == "version").Value,
            [.. archive.Entries.Select(entry => entry.FullName)],
            manifest);
    }
}
