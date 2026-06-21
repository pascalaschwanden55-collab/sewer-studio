using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCatalogPathResolverTests
{
    [Fact]
    public void Resolve_prefers_explicit_section_and_node_catalog_files()
    {
        using var temp = new TempCatalogRoot();
        var settings = new AppSettings
        {
            VsaCatalogSecXmlPath = temp.SectionCatalogPath,
            VsaCatalogNodXmlPath = temp.NodeCatalogPath
        };

        var resolved = VsaCatalogPathResolver.Resolve(settings, temp.BaseDirectory, _ => null);

        Assert.Equal(temp.SectionCatalogPath, resolved.SectionCatalogPath);
        Assert.Equal(temp.NodeCatalogPath, resolved.NodeCatalogPath);
        Assert.Equal(new[] { temp.SectionCatalogPath, temp.NodeCatalogPath }, resolved.XmlCatalogPaths);
        Assert.Equal(string.Join(" | ", resolved.SourcePaths), resolved.DisplayPath);
    }

    [Fact]
    public void Resolve_uses_environment_roots_when_settings_are_empty()
    {
        using var temp = new TempCatalogRoot();
        var env = new Dictionary<string, string?>
        {
            [VsaCatalogPathResolver.SectionCatalogRootEnvVar] = temp.CatalogRoot,
            [VsaCatalogPathResolver.NodeCatalogRootEnvVar] = temp.CatalogRoot
        };

        var resolved = VsaCatalogPathResolver.Resolve(new AppSettings(), temp.BaseDirectory, name => env.GetValueOrDefault(name));

        Assert.Equal(temp.SectionCatalogPath, resolved.SectionCatalogPath);
        Assert.Equal(temp.NodeCatalogPath, resolved.NodeCatalogPath);
    }

    [Fact]
    public void Resolve_includes_kek_manifest_from_data_directory_before_xml_catalogs()
    {
        using var temp = new TempCatalogRoot();
        var settings = new AppSettings
        {
            VsaCatalogSecXmlPath = temp.SectionCatalogPath,
            VsaCatalogNodXmlPath = temp.NodeCatalogPath
        };

        var resolved = VsaCatalogPathResolver.Resolve(settings, temp.BaseDirectory, _ => null);

        Assert.Equal(temp.KekManifestPath, resolved.KekManifestPath);
        Assert.Equal(temp.KekManifestPath, resolved.SourcePaths[0]);
        Assert.Equal(temp.SectionCatalogPath, resolved.SourcePaths[1]);
        Assert.Equal(temp.NodeCatalogPath, resolved.SourcePaths[2]);
    }

    [Fact]
    public void ResolveTextFallbackPath_finds_section_catalog_next_to_configured_file()
    {
        using var temp = new TempCatalogRoot();
        var settings = new AppSettings
        {
            VsaCatalogSecXmlPath = temp.SectionCatalogPath
        };

        var resolved = VsaCatalogPathResolver.ResolveTextFallbackPath(settings, temp.SectionCatalogPath);

        Assert.Equal(temp.SectionCatalogPath, resolved);
    }

    private sealed class TempCatalogRoot : IDisposable
    {
        public TempCatalogRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "SewerStudioVsaCatalogTests", Guid.NewGuid().ToString("N"));
            CatalogRoot = Path.Combine(Root, "Catalogs");
            BaseDirectory = Path.Combine(Root, "App");
            Directory.CreateDirectory(CatalogRoot);
            Directory.CreateDirectory(Path.Combine(BaseDirectory, "Data"));

            SectionCatalogPath = Path.Combine(CatalogRoot, Vsa2019CatalogResolver.SectionCatalogFileName);
            NodeCatalogPath = Path.Combine(CatalogRoot, Vsa2019CatalogResolver.NodeCatalogFileName);
            KekManifestPath = Path.Combine(BaseDirectory, "Data", VsaCatalogPathResolver.KekManifestFileName);

            File.WriteAllText(SectionCatalogPath, "<sec />");
            File.WriteAllText(NodeCatalogPath, "<nod />");
            File.WriteAllText(KekManifestPath, "{}");
        }

        public string Root { get; }
        public string CatalogRoot { get; }
        public string BaseDirectory { get; }
        public string SectionCatalogPath { get; }
        public string NodeCatalogPath { get; }
        public string KekManifestPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
