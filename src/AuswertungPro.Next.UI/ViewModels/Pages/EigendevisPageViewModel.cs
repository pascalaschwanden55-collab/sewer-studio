using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class EigendevisPageViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;

    [ObservableProperty] private string _summary = "Noch kein Eigendevis berechnet.";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _totalBaumeister = "CHF 0.00";
    [ObservableProperty] private string _totalRohrleitungsbau = "CHF 0.00";
    [ObservableProperty] private string _totalGesamt = "CHF 0.00";
    [ObservableProperty] private bool _hasResult;

    public ObservableCollection<DevisTreeNode> BaumeisterNodes { get; } = [];
    public ObservableCollection<DevisTreeNode> RohrleitungsbauNodes { get; } = [];

    private DevisErgebnis? _ergebnis;

    public IRelayCommand GenerateCommand { get; }
    public IRelayCommand ExportOhnePreisCommand { get; }
    public IRelayCommand ExportMitKVCommand { get; }

    public EigendevisPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell;
        _sp = sp;
        GenerateCommand = new RelayCommand(Generate);
        ExportOhnePreisCommand = new RelayCommand(ExportOhnePreis, () => _ergebnis is not null);
        ExportMitKVCommand = new RelayCommand(ExportMitKV, () => _ergebnis is not null);
    }

    private void Generate()
    {
        var haltungen = ExtractHaltungen();
        if (haltungen.Count == 0)
        {
            Summary = "Keine Haltungen mit Schadenscodes gefunden.";
            _shell.SetStatus("Eigendevis: Keine Daten");
            return;
        }

        var baustelle = _shell.Project.Name ?? "Sanierungsprojekt";
        var zone = _shell.Project.Metadata.TryGetValue("Zone", out var z) ? z : "";

        var ergebnis = _sp.DevisGenerator.Generate(baustelle, zone, haltungen);
        _ergebnis = ergebnis;

        TotalBaumeister = FormatChf(ergebnis.Baumeister.GesamttotalInklMwst);
        TotalRohrleitungsbau = FormatChf(ergebnis.Rohrleitungsbau.GesamttotalInklMwst);
        TotalGesamt = FormatChf(ergebnis.Baumeister.GesamttotalInklMwst + ergebnis.Rohrleitungsbau.GesamttotalInklMwst);

        BaumeisterNodes.Clear();
        foreach (var node in BuildTree(ergebnis.Baumeister))
            BaumeisterNodes.Add(node);

        RohrleitungsbauNodes.Clear();
        foreach (var node in BuildTree(ergebnis.Rohrleitungsbau))
            RohrleitungsbauNodes.Add(node);

        HasResult = true;

        var warnText = ergebnis.Warnungen.Count > 0
            ? $"\n{ergebnis.Warnungen.Count} Warnung(en):\n" + string.Join("\n", ergebnis.Warnungen.Take(5))
            : "";

        Summary = $"Eigendevis generiert: {haltungen.Count} Haltungen verarbeitet.\n" +
                  $"Baumeister: {TotalBaumeister}  |  Rohrleitungsbau: {TotalRohrleitungsbau}\n" +
                  $"Gesamt: {TotalGesamt}" + warnText;

        _shell.SetStatus($"Eigendevis: {TotalGesamt}");
        ((RelayCommand)ExportOhnePreisCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ExportMitKVCommand).NotifyCanExecuteChanged();
    }

    private void ExportOhnePreis() => DoExport(showPreise: false);
    private void ExportMitKV() => DoExport(showPreise: true);

    private void DoExport(bool showPreise)
    {
        if (_ergebnis is null) return;

        var suffix = showPreise ? "mit_KV" : "ohne_Preis";
        var defaultName = $"Eigendevis_{suffix}";
        var path = _sp.Dialogs.SaveFile($"Eigendevis exportieren ({suffix})", "Excel (*.xlsx)|*.xlsx", ".xlsx", defaultName);
        if (path is null) return;

        try
        {
            // Export both Gewerke into one file each
            var dir = Path.GetDirectoryName(path) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(path);

            var bmPath = Path.Combine(dir, $"{baseName}_Baumeister.xlsx");
            var rlPath = Path.Combine(dir, $"{baseName}_Rohrleitungsbau.xlsx");

            _sp.DevisExcelExporter.Export(_ergebnis.Baumeister, bmPath);
            _sp.DevisExcelExporter.Export(_ergebnis.Rohrleitungsbau, rlPath);

            _shell.SetStatus($"Eigendevis exportiert: {bmPath}");
        }
        catch (Exception ex)
        {
            _shell.SetStatus($"Export-Fehler: {ex.Message}");
        }
    }

    private List<HaltungMitSchaeden> ExtractHaltungen()
    {
        var result = new List<HaltungMitSchaeden>();

        foreach (var record in _shell.Project.Data)
        {
            var findings = record.VsaFindings;
            if (findings is null || findings.Count == 0)
                continue;

            var dnStr = record.GetFieldValue("DN_mm") ?? record.GetFieldValue("Nennweite");
            int.TryParse(dnStr, out var dn);
            if (dn == 0) dn = 200;

            var laengeStr = record.GetFieldValue("Haltungslaenge_m") ?? record.GetFieldValue("Laenge_m");
            decimal.TryParse(laengeStr?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var laenge);
            if (laenge == 0) laenge = 50;

            var zkStr = record.GetFieldValue("VSA_Zustandsklasse") ?? record.GetFieldValue("Zustandsklasse");
            int.TryParse(zkStr, out var zk);

            var vonSchacht = record.GetFieldValue("Schacht_oben") ?? record.GetFieldValue("Von_Schacht") ?? "";
            var bisSchacht = record.GetFieldValue("Schacht_unten") ?? record.GetFieldValue("Bis_Schacht") ?? "";
            var material = record.GetFieldValue("Rohrmaterial") ?? "";

            var schaeden = findings.Select(f => new SchadenInfo
            {
                Code = f.KanalSchadencode ?? "",
                Char1 = f.Quantifizierung1,
                Char2 = f.Quantifizierung2,
                MeterStart = (decimal?)f.SchadenlageAnfang,
                MeterEnd = (decimal?)f.SchadenlageEnde,
                Zustandsklasse = zk
            }).Where(s => !string.IsNullOrWhiteSpace(s.Code)).ToList();

            if (schaeden.Count == 0) continue;

            result.Add(new HaltungMitSchaeden
            {
                HaltungsId = record.Id.ToString(),
                VonSchacht = vonSchacht,
                BisSchacht = bisSchacht,
                DN = dn,
                Laenge = laenge,
                Material = material,
                Zustandsklasse = zk,
                Schaeden = schaeden
            });
        }

        return result;
    }

    private static List<DevisTreeNode> BuildTree(Eigendevis devis)
    {
        var nodes = new List<DevisTreeNode>();
        foreach (var hg in devis.Hauptgruppen)
        {
            var gruppeNode = new DevisTreeNode
            {
                Label = $"{hg.Nummer}  {hg.Bezeichnung}",
                Betrag = hg.Total,
                IsGruppe = true
            };

            // Abschnitte
            foreach (var abschnitt in hg.Abschnitte)
            {
                var abschnittNode = new DevisTreeNode
                {
                    Label = abschnitt.Bezeichnung,
                    Betrag = abschnitt.Total,
                    IsAbschnitt = true
                };

                foreach (var pos in abschnitt.Positionen)
                    abschnittNode.Children.Add(PositionToNode(pos));

                gruppeNode.Children.Add(abschnittNode);
            }

            // Einzelpositionen
            foreach (var pos in hg.Positionen)
                gruppeNode.Children.Add(PositionToNode(pos));

            nodes.Add(gruppeNode);
        }
        return nodes;
    }

    private static DevisTreeNode PositionToNode(DevisPosition pos)
    {
        return new DevisTreeNode
        {
            Label = pos.Bezeichnung,
            PositionNr = pos.PositionNummer,
            Menge = pos.Menge,
            Einheit = pos.Einheit,
            Einheitspreis = pos.Einheitspreis,
            Betrag = pos.Betrag,
            Konfidenz = pos.Herleitung?.Konfidenz ?? ConfidenceLevel.Manual,
            Formel = pos.Herleitung?.Formel ?? ""
        };
    }

    private static string FormatChf(decimal value)
        => $"CHF {value:N2}";
}

public sealed class DevisTreeNode
{
    public string Label { get; set; } = "";
    public string PositionNr { get; set; } = "";
    public decimal Menge { get; set; }
    public string Einheit { get; set; } = "";
    public decimal Einheitspreis { get; set; }
    public decimal Betrag { get; set; }
    public bool IsGruppe { get; set; }
    public bool IsAbschnitt { get; set; }
    public ConfidenceLevel Konfidenz { get; set; }
    public string Formel { get; set; } = "";
    public string BetragFormatiert => $"CHF {Betrag:N2}";
    public ObservableCollection<DevisTreeNode> Children { get; } = [];

    public string KonfidenzFarbe => Konfidenz switch
    {
        ConfidenceLevel.High => "#16A34A",
        ConfidenceLevel.Medium => "#F59E0B",
        ConfidenceLevel.Low => "#DC2626",
        ConfidenceLevel.Manual => "#9CA3AF",
        _ => "#9CA3AF"
    };
}
