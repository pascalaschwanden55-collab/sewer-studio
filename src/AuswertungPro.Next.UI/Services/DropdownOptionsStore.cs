using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services;

public sealed class DropdownOptionsModel
{
    public List<string> SanierenOptions { get; set; } = new();
    public List<string> EigentuemerOptions { get; set; } = new();
    public List<string> PruefungsresultatOptions { get; set; } = new() { "" };
    public List<string> ReferenzpruefungOptions { get; set; } = new() { "" };
    public List<string> EmpfohleneSanierungsmassnahmenOptions { get; set; } = new() { "" };

    /// <summary>Nur die selbst ergaenzten Rohrmaterialien; die festen Katalogwerte stehen im Feldkatalog.</summary>
    public List<string> RohrmaterialOptions { get; set; } = new();
}

public interface IDropdownOptionsStore
{
    IReadOnlyList<string> FixedEigentuemerOptions { get; }
    DropdownOptionsModel LoadOrDefault();
    void Save(DropdownOptionsModel model);
    List<string> LoadSanierenOptions();
    void SaveSanierenOptions(IEnumerable<string> options);
    List<string> LoadEigentuemerOptions();
    void SaveEigentuemerOptions(IEnumerable<string> options);
    List<string> LoadPruefungsresultatOptions();
    void SavePruefungsresultatOptions(IEnumerable<string> options);
    List<string> LoadReferenzpruefungOptions();
    void SaveReferenzpruefungOptions(IEnumerable<string> options);
    List<string> LoadEmpfohleneSanierungsmassnahmenOptions();
    void SaveEmpfohleneSanierungsmassnahmenOptions(IEnumerable<string> options);
    List<string> LoadRohrmaterialOptions();
    void SaveRohrmaterialOptions(IEnumerable<string> options);
}

/// <summary>Dateibasierter, atomar schreibender Speicher fuer die editierbaren Auswahllisten.</summary>
public sealed class FileDropdownOptionsStore : IDropdownOptionsStore
{
    /// <summary>
    /// Die Eigentuemer des Abwassernetzes im Kanton Uri — der ganze Bestand, nicht
    /// nur Sammelbegriffe.
    ///
    /// Der XTF-Export schreibt den Eigentuemer zeichengleich in die Datei. Stuende in
    /// der Auswahl nur "Gemeinde", entstuende dort auch nur "Gemeinde" statt der
    /// Gemeinde, der die Leitung gehoert.
    ///
    /// GEMESSEN am 2026-09-02 an `org_eigentuemer` in den lokalen QGIS-Kopien:
    /// 110297 Leitungen und 68735 Schaechte fuehren exakt dieselben 27 Werte — kein
    /// Wert kommt nur auf einer Seite vor. Genau drei Gemeinden tragen den
    /// Kantonszusatz (Altdorf, Buerglen, Seedorf — die Namen gibt es auch in anderen
    /// Kantonen), die uebrigen 16 stehen ohne. Eine frueher hier genannte Stichprobe
    /// von 3000 Leitungen ist damit ueberholt.
    ///
    /// Die sechs Sammelbegriffe bleiben vorn stehen: Altprojekte fuehren sie, und
    /// beide Excel-Vorlagen faerben genau sie. Die Kurzformen "AWU" und "Kanton"
    /// stehen nicht zur Auswahl, bleiben dort aber gueltig und werden mitgezaehlt.
    ///
    /// Jeder Eintrag muss einen Organisationstyp haben — ohne ihn laesst der
    /// XTF-Export den gewaehlten Wert still liegen. `DropdownOptionListTests` haelt
    /// das fest.
    /// </summary>
    private static readonly IReadOnlyList<string> FixedOwners =
        new[]
        {
            "Privat", "Abwasser Uri", "Gemeinde", "Kanton Uri", "Bund", "unbekannt",
            "ASTRA - Bundesamt für Strassen",
            "Korporation Uri",
            "Meliorationsgenossenschaft Reussebene Uri",
            "Meliorationsgesellschaft Seedorf",
            "Altdorf (UR)", "Andermatt", "Attinghausen", "Bürglen (UR)", "Erstfeld",
            "Flüelen", "Göschenen", "Gurtnellen", "Hospental", "Isenthal", "Realp",
            "Schattdorf", "Seedorf (UR)", "Seelisberg", "Silenen", "Sisikon",
            "Spiringen", "Unterschächen", "Wassen"
        };

    private readonly string _optionsDir;
    private readonly string _legacyOptionsDir;
    private readonly string _legacyOptionsPath;
    private readonly object _sync = new();
    private bool _migrated;

    public FileDropdownOptionsStore()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName, "dropdowns"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.LegacyRoamingDataFolder, "dropdowns"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.LegacyRoamingDataFolder, "dropdowns.json"))
    {
    }

    public FileDropdownOptionsStore(string optionsDir, string legacyOptionsDir, string legacyOptionsPath)
    {
        _optionsDir = NormalizeRequiredPath(optionsDir, nameof(optionsDir));
        _legacyOptionsDir = NormalizeRequiredPath(legacyOptionsDir, nameof(legacyOptionsDir));
        _legacyOptionsPath = NormalizeRequiredPath(legacyOptionsPath, nameof(legacyOptionsPath));
    }

    public IReadOnlyList<string> FixedEigentuemerOptions => FixedOwners;

    public DropdownOptionsModel LoadOrDefault()
        => new()
        {
            SanierenOptions = LoadSanierenOptions(),
            EigentuemerOptions = LoadEigentuemerOptions(),
            PruefungsresultatOptions = LoadPruefungsresultatOptions(),
            ReferenzpruefungOptions = LoadReferenzpruefungOptions(),
            EmpfohleneSanierungsmassnahmenOptions = LoadEmpfohleneSanierungsmassnahmenOptions(),
            RohrmaterialOptions = LoadRohrmaterialOptions()
        };

    public void Save(DropdownOptionsModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SaveSanierenOptions(model.SanierenOptions);
        SaveEigentuemerOptions(model.EigentuemerOptions);
        SavePruefungsresultatOptions(model.PruefungsresultatOptions);
        SaveReferenzpruefungOptions(model.ReferenzpruefungOptions);
        SaveEmpfohleneSanierungsmassnahmenOptions(model.EmpfohleneSanierungsmassnahmenOptions);
        SaveRohrmaterialOptions(model.RohrmaterialOptions);
    }

    public List<string> LoadSanierenOptions()
        => LoadList("sanieren", DefaultModel().SanierenOptions);

    public void SaveSanierenOptions(IEnumerable<string> options)
        => SaveList("sanieren", options);

    public List<string> LoadEigentuemerOptions()
        => new(FixedEigentuemerOptions);

    public void SaveEigentuemerOptions(IEnumerable<string> options)
        => SaveList("eigentuemer", FixedEigentuemerOptions);

    public List<string> LoadPruefungsresultatOptions()
        => LoadList("pruefungsresultat", DefaultModel().PruefungsresultatOptions);

    public void SavePruefungsresultatOptions(IEnumerable<string> options)
        => SaveList("pruefungsresultat", options);

    public List<string> LoadReferenzpruefungOptions()
        => LoadList("referenzpruefung", DefaultModel().ReferenzpruefungOptions);

    public void SaveReferenzpruefungOptions(IEnumerable<string> options)
        => SaveList("referenzpruefung", options);

    public List<string> LoadEmpfohleneSanierungsmassnahmenOptions()
        => LoadList("sanierungsmassnahmen", DefaultModel().EmpfohleneSanierungsmassnahmenOptions);

    public void SaveEmpfohleneSanierungsmassnahmenOptions(IEnumerable<string> options)
        => SaveList("sanierungsmassnahmen", options);

    // Gespeichert werden bewusst nur die selbst ergaenzten Materialien. Die festen
    // Katalogwerte kommen bei jedem Start aus dem Feldkatalog und koennen dadurch
    // weder geloescht noch durch eine alte Datei ueberholt werden.
    public List<string> LoadRohrmaterialOptions()
        => LoadList("rohrmaterial", DefaultModel().RohrmaterialOptions);

    public void SaveRohrmaterialOptions(IEnumerable<string> options)
        => SaveList("rohrmaterial", options);

    private List<string> LoadList(string key, List<string> defaults)
    {
        lock (_sync)
        {
            EnsureMigrated();
            try
            {
                Directory.CreateDirectory(_optionsDir);
                var path = Path.Combine(_optionsDir, $"{key}.json");
                if (!File.Exists(path))
                    return new List<string>(defaults);

                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<string>>(
                    json,
                    Application.Common.JsonDefaults.CaseInsensitive);

                if (list is null || list.Count == 0)
                    return new List<string>(defaults);

                if (list.All(x => string.IsNullOrWhiteSpace(x))
                    && defaults.Any(x => !string.IsNullOrWhiteSpace(x)))
                    return new List<string>(defaults);

                return list;
            }
            catch
            {
                return new List<string>(defaults);
            }
        }
    }

    private void SaveList(string key, IEnumerable<string> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_sync)
        {
            Directory.CreateDirectory(_optionsDir);
            var path = Path.Combine(_optionsDir, $"{key}.json");
            var json = JsonSerializer.Serialize(options, Application.Common.JsonDefaults.Indented);
            Application.Common.AtomicTextFileWriter.WriteAllText(path, json);
        }
    }

    private void EnsureMigrated()
    {
        if (_migrated)
            return;

        lock (_sync)
        {
            if (_migrated)
                return;
            _migrated = true;

            try
            {
                if (Directory.Exists(_legacyOptionsDir) && !Directory.Exists(_optionsDir))
                {
                    Directory.CreateDirectory(_optionsDir);
                    foreach (var legacyFile in Directory.EnumerateFiles(
                                 _legacyOptionsDir,
                                 "*.json",
                                 SearchOption.TopDirectoryOnly))
                    {
                        var destination = Path.Combine(_optionsDir, Path.GetFileName(legacyFile));
                        if (!File.Exists(destination))
                            File.Copy(legacyFile, destination, overwrite: false);
                    }
                }
            }
            catch
            {
                // Alte Optionsdateien sind optional; sichere Standardwerte bleiben verfuegbar.
            }

            if (!File.Exists(_legacyOptionsPath))
                return;

            try
            {
                var json = File.ReadAllText(_legacyOptionsPath);
                var model = JsonSerializer.Deserialize<DropdownOptionsModel>(
                    json,
                    Application.Common.JsonDefaults.CaseInsensitive);
                if (model is null)
                    return;

                if (model.SanierenOptions.Count > 0)
                    SaveSanierenOptions(model.SanierenOptions);
                if (model.EigentuemerOptions.Count > 0)
                    SaveEigentuemerOptions(model.EigentuemerOptions);
                if (model.PruefungsresultatOptions.Count > 0)
                    SavePruefungsresultatOptions(model.PruefungsresultatOptions);
                if (model.ReferenzpruefungOptions.Count > 0)
                    SaveReferenzpruefungOptions(model.ReferenzpruefungOptions);
                if (model.EmpfohleneSanierungsmassnahmenOptions.Count > 0)
                    SaveEmpfohleneSanierungsmassnahmenOptions(model.EmpfohleneSanierungsmassnahmenOptions);
                if (model.RohrmaterialOptions.Count > 0)
                    SaveRohrmaterialOptions(model.RohrmaterialOptions);
            }
            catch
            {
                // Beschaedigte Altdateien werden ignoriert; sichere Standardwerte bleiben verfuegbar.
            }
        }
    }

    private DropdownOptionsModel DefaultModel()
        => new()
        {
            SanierenOptions = new List<string> { "Ja", "Nein" },
            EigentuemerOptions = new List<string>(FixedEigentuemerOptions),
            PruefungsresultatOptions = new List<string>
            {
                "Pruefung bestanden",
                "Pruefung knapp nicht bestanden",
                "Pruefung nicht bestanden (grob undicht)",
                "Keine"
            },
            ReferenzpruefungOptions = new List<string> { "Ja", "Nein" },
            EmpfohleneSanierungsmassnahmenOptions = new List<string>
            {
                "",
                "Schlauchliner (Nadelfilz) DN 100-200",
                "Schlauchliner (GFK) DN 200-300",
                "Kurzliner / Partliner",
                "Manschette (Quick Lock)",
                "Manschette (Quick Lock EPDM)",
                "Pointliner",
                "Anschluss verpressen",
                "Reinigung + TV-Inspektion",
                "Erneuerung / Neubau"
            },
            RohrmaterialOptions = new List<string>()
        };

    private static string NormalizeRequiredPath(string path, string parameterName)
        => string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("Speicherpfad fehlt.", parameterName)
            : Path.GetFullPath(path);
}

/// <summary>Kompatibilitaetsfassade fuer bestehende Aufrufer.</summary>
public static class DropdownOptionsStore
{
    private static readonly IDropdownOptionsStore Default = new FileDropdownOptionsStore();

    public static IReadOnlyList<string> FixedEigentuemerOptions => Default.FixedEigentuemerOptions;
    public static DropdownOptionsModel LoadOrDefault() => Default.LoadOrDefault();
    public static void Save(DropdownOptionsModel model) => Default.Save(model);
    public static List<string> LoadSanierenOptions() => Default.LoadSanierenOptions();
    public static void SaveSanierenOptions(IEnumerable<string> options) => Default.SaveSanierenOptions(options);
    public static List<string> LoadEigentuemerOptions() => Default.LoadEigentuemerOptions();
    public static void SaveEigentuemerOptions(IEnumerable<string> options) => Default.SaveEigentuemerOptions(options);
    public static List<string> LoadPruefungsresultatOptions() => Default.LoadPruefungsresultatOptions();
    public static void SavePruefungsresultatOptions(IEnumerable<string> options) => Default.SavePruefungsresultatOptions(options);
    public static List<string> LoadReferenzpruefungOptions() => Default.LoadReferenzpruefungOptions();
    public static void SaveReferenzpruefungOptions(IEnumerable<string> options) => Default.SaveReferenzpruefungOptions(options);
    public static List<string> LoadEmpfohleneSanierungsmassnahmenOptions() => Default.LoadEmpfohleneSanierungsmassnahmenOptions();
    public static void SaveEmpfohleneSanierungsmassnahmenOptions(IEnumerable<string> options) => Default.SaveEmpfohleneSanierungsmassnahmenOptions(options);
    public static List<string> LoadRohrmaterialOptions() => Default.LoadRohrmaterialOptions();
    public static void SaveRohrmaterialOptions(IEnumerable<string> options) => Default.SaveRohrmaterialOptions(options);
}
