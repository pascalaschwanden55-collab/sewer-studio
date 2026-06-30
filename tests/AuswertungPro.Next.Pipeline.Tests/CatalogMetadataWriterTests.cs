using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer CatalogMetadataWriter (IST-Verhalten aus ProtocolCodePickerViewModel).
/// </summary>
public sealed class CatalogMetadataWriterTests
{
    private static CodeDefinition MakeDef(string? source = null, string? canonical = null, string? annotation = null)
        => new()
        {
            Code = "BAB",
            Title = "Riss",
            Source = source,
            CanonicalCode = canonical,
            StandardAnnotation = annotation
        };

    [Fact]
    public void AddCatalogMetadata_leer_def_schreibt_nichts_in_dict()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef());
        Assert.Empty(parameters);
    }

    [Fact]
    public void AddCatalogMetadata_source_wird_eingetragen()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef(source: "ILI"));
        Assert.Equal("ILI", parameters["catalog.source"]);
    }

    [Fact]
    public void AddCatalogMetadata_canonical_code_wird_eingetragen()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef(canonical: "BAB.A"));
        Assert.Equal("BAB.A", parameters["catalog.canonicalCode"]);
    }

    [Fact]
    public void AddCatalogMetadata_standard_annotation_wird_eingetragen()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef(annotation: "Laengsriss"));
        Assert.Equal("Laengsriss", parameters["catalog.standardAnnotation"]);
    }

    [Fact]
    public void AddCatalogMetadata_alle_felder_werden_zusammen_eingetragen()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef("ILI", "BAB.A", "Laengsriss"));
        Assert.Equal(3, parameters.Count);
        Assert.Equal("ILI", parameters["catalog.source"]);
        Assert.Equal("BAB.A", parameters["catalog.canonicalCode"]);
        Assert.Equal("Laengsriss", parameters["catalog.standardAnnotation"]);
    }

    [Fact]
    public void AddCatalogMetadata_leerstring_source_wird_nicht_eingetragen()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef(source: "   "));
        Assert.DoesNotContain("catalog.source", parameters.Keys);
    }

    [Fact]
    public void AddCatalogMetadata_wert_wird_getrimmt()
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CatalogMetadataWriter.AddCatalogMetadata(parameters, MakeDef(source: "  ILI  "));
        Assert.Equal("ILI", parameters["catalog.source"]);
    }
}
