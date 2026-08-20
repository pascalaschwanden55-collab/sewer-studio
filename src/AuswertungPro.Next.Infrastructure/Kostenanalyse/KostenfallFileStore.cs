using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Kostenanalyse;

namespace AuswertungPro.Next.Infrastructure.Kostenanalyse;

/// <summary>
/// Speichert die Faelle als eine JSON-Datei unter &lt;Wurzel&gt;\kostenanalyse\.
///
/// Unterschied zwischen "noch nie gelaufen" und "kaputt" wie bei den uebrigen
/// KI-Dateien des Projekts: Eine fehlende Datei ist leer, eine unlesbare bricht ab und
/// wird NICHT ueberschrieben.
/// </summary>
public sealed class KostenfallFileStore : IKostenfallStore
{
    private const string OrdnerName = "kostenanalyse";
    private const string DateiName = "kostenfaelle_v1.json";

    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _pfad;

    public KostenfallFileStore(string wurzel)
    {
        if (string.IsNullOrWhiteSpace(wurzel))
            throw new ArgumentException("Wurzel fehlt.", nameof(wurzel));

        _pfad = Path.Combine(wurzel, OrdnerName, DateiName);
    }

    public IReadOnlyList<Kostenfall> Lade()
    {
        if (!File.Exists(_pfad))
            return [];

        try
        {
            var inhalt = File.ReadAllText(_pfad);
            return JsonSerializer.Deserialize<List<Kostenfall>>(inhalt, Optionen) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Die Falldatei ist beschaedigt und wurde nicht veraendert: {_pfad}", ex);
        }
    }

    public void Speichere(IReadOnlyList<Kostenfall> faelle)
    {
        ArgumentNullException.ThrowIfNull(faelle);

        var ordner = Path.GetDirectoryName(_pfad)!;
        Directory.CreateDirectory(ordner);

        // Erst danebenschreiben, dann umlegen — ein Absturz darf den Bestand nie halbieren.
        var temp = _pfad + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(faelle, Optionen));
        File.Move(temp, _pfad, overwrite: true);
    }
}
