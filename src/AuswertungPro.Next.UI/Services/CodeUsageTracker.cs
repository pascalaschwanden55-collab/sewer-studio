using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// JSON-persistenter Nutzungszaehler fuer VSA-Codes (Muster wie DropdownOptionsStore:
/// Ablage unter %AppData%). Kaputte Dateien werden toleriert (leerer Start),
/// die Historie ist gedeckelt, damit die Datei klein bleibt.
/// </summary>
public sealed class CodeUsageTracker : ICodeUsageTracker
{
    private const int MaxHistorie = 500;

    private readonly string _dateiPfad;
    private readonly object _sync = new();
    private readonly Dictionary<string, int> _anzahlProCode;
    private readonly List<string> _historie; // neueste am Ende

    public CodeUsageTracker()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppIdentity.ProductName,
            "code-usage.json"))
    {
    }

    public CodeUsageTracker(string dateiPfad)
    {
        _dateiPfad = dateiPfad;
        (_anzahlProCode, _historie) = Laden(dateiPfad);
    }

    public void Erfasse(string? code)
    {
        var normalisiert = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalisiert))
            return;

        lock (_sync)
        {
            _anzahlProCode[normalisiert] = _anzahlProCode.TryGetValue(normalisiert, out var n) ? n + 1 : 1;
            _historie.Add(normalisiert);
            if (_historie.Count > MaxHistorie)
                _historie.RemoveRange(0, _historie.Count - MaxHistorie);
            Speichern();
        }
    }

    public IReadOnlyList<CodeUsageEintrag> TopCodes(int n)
    {
        lock (_sync)
        {
            return _anzahlProCode
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(Math.Max(0, n))
                .Select(kv => new CodeUsageEintrag(kv.Key, kv.Value))
                .ToList();
        }
    }

    public IReadOnlyList<string> Zuletzt(int n)
    {
        lock (_sync)
        {
            return _historie
                .AsEnumerable()
                .Reverse()
                .Distinct(StringComparer.Ordinal)
                .Take(Math.Max(0, n))
                .ToList();
        }
    }

    private void Speichern()
    {
        try
        {
            var ordner = Path.GetDirectoryName(_dateiPfad);
            if (!string.IsNullOrEmpty(ordner))
                Directory.CreateDirectory(ordner);

            var daten = new PersistenzModell
            {
                Anzahl = _anzahlProCode,
                Historie = _historie
            };
            File.WriteAllText(_dateiPfad, JsonSerializer.Serialize(daten));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Favoriten sind Komfort — ein Schreibfehler darf die Codierung nie stoppen.
        }
    }

    private static (Dictionary<string, int>, List<string>) Laden(string pfad)
    {
        try
        {
            if (!File.Exists(pfad))
                return (new Dictionary<string, int>(StringComparer.Ordinal), []);

            var daten = JsonSerializer.Deserialize<PersistenzModell>(File.ReadAllText(pfad));
            return (
                new Dictionary<string, int>(daten?.Anzahl ?? [], StringComparer.Ordinal),
                daten?.Historie ?? []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new Dictionary<string, int>(StringComparer.Ordinal), []);
        }
    }

    private sealed class PersistenzModell
    {
        public Dictionary<string, int> Anzahl { get; set; } = new(StringComparer.Ordinal);
        public List<string> Historie { get; set; } = [];
    }
}
