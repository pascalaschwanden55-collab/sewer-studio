using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DossiersPageViewModel
{
    // ── Anlegen, aendern, loeschen ────────────────────────────────────────

    private async Task CreateDossierAsync()
    {
        if (!EnsureProject(out var root))
            return;

        // Zuerst Gemeinde und Parzelle: daraus fuellt der Kanton alles vor, was
        // er hergibt. Wer das nicht will, legt ohne Abfrage an.
        var idsByName = HaltungsIdsNachName();

        DossierParcelLookupChoice? abfrage;
        try
        {
            abfrage = _dialogWindows.NewProperty(idsByName, ProjektSchachtnummern());
        }
        catch (Exception ex)
        {
            StatusMessage = "Die Abfrage konnte nicht geöffnet werden: " + ex.Message;
            _dialogs.Error(StatusMessage, "Neue Liegenschaft");
            return;
        }

        if (abfrage is null)
            return;

        var definition = abfrage.Dossier;

        foreach (var bezeichnung in abfrage.SelectedHoldingDesignations)
        {
            if (idsByName.TryGetValue(bezeichnung, out var id)
                && !definition.HoldingIds.Contains(id))
            {
                definition.HoldingIds.Add(id);
            }
        }

        definition.ShaftNumbers = abfrage.ShaftNumbers.ToList();

        if (!_dialogWindows.EditDossier(definition, isNew: true))
            return;

        definition.FolderName = DossierFolderPlanner.PlanFolderName(
            definition.Name,
            candidate => _document.Dossiers.Any(d =>
                string.Equals(d.FolderName, candidate, StringComparison.OrdinalIgnoreCase))
                || Directory.Exists(Path.Combine(
                    DossierFolderPlanner.ResolveRoot(root), candidate)));

        _document.Dossiers.Add(definition);

        if (!await SaveDocumentAsync(root))
        {
            _document.Dossiers.Remove(definition);
            return;
        }

        RebuildList();
        Selected = Dossiers.FirstOrDefault(d => d.Id == definition.Id);
        StatusMessage = $"Dossier „{definition.Name}\" angelegt.";
    }

    private async Task EditDossierAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        // Das Fenster aendert die Angaben unmittelbar. Ohne Vorherstand
        // stuenden nach einem misslungenen Speichern Angaben am Bildschirm,
        // die nicht auf der Platte sind.
        var vorher = DossierDeepCopy.Of(Selected.Definition);

        if (!_dialogWindows.EditDossier(Selected.Definition, isNew: false))
        {
            ErsetzeDossier(Selected.Definition, vorher);
            RebuildList();
            return;
        }

        Selected.Definition.ModifiedAtUtc = DateTime.UtcNow;
        if (!await SaveDocumentAsync(root))
        {
            ErsetzeDossier(Selected.Definition, vorher);
            RebuildList();
            return;
        }

        RebuildList();
        StatusMessage = "Stammdaten gespeichert.";
    }

    private async Task EditAreaAsync()
    {
        if (!EnsureProject(out var root))
            return;

        var vorher = DossierDeepCopy.Of(_document.Area);

        if (!_dialogWindows.EditArea(_document.Area))
        {
            _document.Area = vorher;
            return;
        }

        if (!await SaveDocumentAsync(root))
        {
            _document.Area = vorher;
            AreaTitle = _document.Area.AreaTitle;
            RefreshDetail();
            return;
        }

        AreaTitle = _document.Area.AreaTitle;
        RefreshDetail();
        StatusMessage = "Gebietsangaben gespeichert. Sie gelten für alle Dossiers.";
    }

    /// <summary>
    /// Setzt ein Dossier auf seinen Vorherstand zurueck — an derselben Stelle
    /// der Liste, damit die Reihenfolge in der Datei erhalten bleibt.
    /// </summary>
    private void ErsetzeDossier(DossierDefinition ziel, DossierDefinition vorher)
    {
        var stelle = _document.Dossiers.IndexOf(ziel);
        if (stelle >= 0)
            _document.Dossiers[stelle] = vorher;
    }

    /// <summary>
    /// Legt fuer die Parzellen des Projekts auf einmal Dossiers an. Die Regeln
    /// liegen in den Anwendungsfaellen; hier wird nur eingesammelt, das Fenster
    /// gezeigt und einmal gespeichert.
    /// </summary>
    private async Task CreateFromProjectAsync()
    {
        if (!EnsureProject(out var root))
            return;

        var project = _getProject();

        // Haltungsname -> Kennung. Ohne Namen laesst sich nichts zuordnen.
        var idsByName = HaltungsIdsNachName();

        if (idsByName.Count == 0)
        {
            StatusMessage = "Das Projekt enthält keine Leitungen — es gibt nichts zu suchen.";
            return;
        }

        // Parzellen, fuer die es schon ein Dossier gibt, werden nicht erneut angeboten.
        // "439, 440" oder "762+756": das Feld ist Freitext. Jede einzelne Nummer
        // muss den Doppelten-Schutz ausloesen, nicht nur die ganze Zeichenkette.
        var mitDossier = _document.Dossiers
            .SelectMany(d => (d.ParcelNumbers ?? string.Empty)
                .Split(new[] { ',', ';', '+', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var erzeugte = _dialogWindows.CreateFromProject(
            idsByName.Keys.ToList(),
            idsByName,
            ProjektSchachtnummern(),
            mitDossier);

        if (erzeugte.Count == 0)
        {
            StatusMessage = "Es wurden keine Dossiers erzeugt.";
            return;
        }

        foreach (var dossier in erzeugte)
        {
            dossier.FolderName = DossierFolderPlanner.PlanFolderName(
                dossier.Name,
                candidate => _document.Dossiers.Any(d =>
                    string.Equals(d.FolderName, candidate, StringComparison.OrdinalIgnoreCase))
                    || Directory.Exists(Path.Combine(
                        DossierFolderPlanner.ResolveRoot(root), candidate)));

            _document.Dossiers.Add(dossier);
        }

        // Alle auf einmal: ein Speichervorgang, nicht einer je Dossier.
        if (!await SaveDocumentAsync(root))
        {
            // Sonst stuenden die neuen Dossiers am Bildschirm, ohne je auf der
            // Platte gewesen zu sein — und der naechste Speichervorgang
            // schriebe sie ungefragt mit.
            foreach (var dossier in erzeugte)
                _document.Dossiers.Remove(dossier);

            RebuildList();
            return;
        }

        await ReloadAsync();
        StatusMessage = erzeugte.Count == 1
            ? "1 Dossier erzeugt."
            : $"{erzeugte.Count} Dossiers erzeugt.";
    }

    private async Task DeleteDossierAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var name = Selected.Name;
        if (!_dialogs.ConfirmWarn(
                $"Das Dossier „{name}\" aus der Liste entfernen?\n\n"
                + "Der Ordner mit Word-Datei und Beilagen bleibt erhalten und wird "
                + "NICHT gelöscht.",
                "Dossier entfernen"))
        {
            return;
        }

        var definition = Selected.Definition;
        var stelle = _document.Dossiers.IndexOf(definition);
        _document.Dossiers.Remove(definition);

        if (!await SaveDocumentAsync(root))
        {
            // An dieselbe Stelle zurueck: hinten angehaengt aenderte sich die
            // Reihenfolge in der Datei ohne jeden Anlass.
            _document.Dossiers.Insert(Math.Clamp(stelle, 0, _document.Dossiers.Count), definition);
            return;
        }

        Selected = null;
        RebuildList();
        StatusMessage = $"Dossier „{name}\" entfernt. Der Ordner blieb erhalten.";
    }

    private bool CanMoveSelectedDossierUp()
        => Selected is not null
           && _document.Dossiers.IndexOf(Selected.Definition) > 0;

    private bool CanMoveSelectedDossierDown()
    {
        if (Selected is null)
            return false;

        var index = _document.Dossiers.IndexOf(Selected.Definition);
        return index >= 0 && index < _document.Dossiers.Count - 1;
    }

    /// <summary>
    /// Verschiebt die gewaehlte Liegenschaft genau eine Stelle. Die Liste in
    /// <c>dossiers.json</c> ist dabei die einzige Reihenfolge; ein zusaetzliches
    /// Sortierfeld waere nur eine zweite Quelle, die auseinanderlaufen kann.
    /// </summary>
    private async Task MoveSelectedDossierAsync(int offset)
    {
        if (Selected is null || !EnsureProject(out var root) || offset is not (-1 or 1))
            return;

        var definition = Selected.Definition;
        var oldIndex = _document.Dossiers.IndexOf(definition);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= _document.Dossiers.Count)
            return;

        _document.Dossiers.RemoveAt(oldIndex);
        _document.Dossiers.Insert(newIndex, definition);

        if (!await SaveDocumentAsync(root))
        {
            _document.Dossiers.RemoveAt(newIndex);
            _document.Dossiers.Insert(oldIndex, definition);
            RebuildList();
            return;
        }

        RebuildList();
        StatusMessage = offset < 0
            ? $"„{definition.Name}“ wurde nach oben verschoben."
            : $"„{definition.Name}“ wurde nach unten verschoben.";
    }

    private async Task EditHoldingsAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var chosen = _dialogWindows.PickHoldings(
            _getProject(), Selected.Definition.HoldingIds);

        if (chosen is null)
            return;

        var previous = new List<Guid>(Selected.Definition.HoldingIds);
        Selected.Definition.HoldingIds = chosen;
        Selected.Definition.ModifiedAtUtc = DateTime.UtcNow;

        if (!await SaveDocumentAsync(root))
        {
            Selected.Definition.HoldingIds = previous;
            return;
        }

        RefreshDetail();
        StatusMessage = chosen.Count == 1
            ? "1 Leitung zugeordnet."
            : $"{chosen.Count} Leitungen zugeordnet.";
    }

    /// <summary>
    /// Waehlt die Schaechte der Liegenschaft. Zwilling zur Leitungsauswahl.
    ///
    /// Gespeichert werden Schachtnummern, nicht Kennungen: so ist derselbe
    /// Schacht auch dann noch gemeint, wenn sein Datensatz spaeter neu
    /// eingelesen wurde.
    /// </summary>
    private async Task EditShaftsAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var chosen = _dialogWindows.PickShafts(
            _getProject(), Selected.Definition.ShaftNumbers);

        if (chosen is null)
            return;

        var vorher = new List<string>(Selected.Definition.ShaftNumbers);
        Selected.Definition.ShaftNumbers = chosen;
        Selected.Definition.ModifiedAtUtc = DateTime.UtcNow;

        if (!await SaveDocumentAsync(root))
        {
            Selected.Definition.ShaftNumbers = vorher;
            return;
        }

        RefreshDetail();
        StatusMessage = SchaechteZugeordnet(chosen.Count);
    }

    private async Task SetDossierStatusAsync(DossierStatus? status)
    {
        if (Selected is null || status is null || !EnsureProject(out var root))
            return;

        var previous = Selected.Definition.Status;
        Selected.Definition.Status = status.Value;

        if (!await SaveDocumentAsync(root))
        {
            Selected.Definition.Status = previous;
            return;
        }

        RefreshDetail();
        RebuildList();
        StatusMessage = "Stand: " + DescribeStatus(status.Value);
    }

    // ── Ausgabe ───────────────────────────────────────────────────────────

    private async Task CreateWordAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var request = BuildRequest(root, Selected.Definition);
            var result = await _wordExport.ExportAsync(request);

            StatusMessage = result.Message;

            if (!result.Success)
            {
                _dialogs.Warn(result.Message, "Word-Datei");
                return;
            }

            if (Selected.Definition.Status == DossierStatus.Offen)
            {
                Selected.Definition.Status = DossierStatus.WordErzeugt;
                await SaveDocumentAsync(root);
                RebuildList();
            }

            _toasts.Success(result.Message);

            if (_dialogs.Confirm(
                    result.Message + "\n\nDatei jetzt in Word öffnen?", "Word-Datei"))
            {
                _shellOpen.TryOpen(result.FilePath!, out _);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CollectAttachmentsAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var request = BuildRequest(root, Selected.Definition);
            var result = await _attachments.CollectAsync(request);

            var original = result.Attachments.Count(a =>
                a.Kind == DossierAttachmentKind.OriginalProtocol);
            var generated = result.Attachments.Count(a =>
                a.Kind == DossierAttachmentKind.GeneratedProtocol);

            var parts = new List<string>();
            if (original > 0)
                parts.Add($"{original}× Original-Protokoll");
            if (generated > 0)
                parts.Add($"{generated}× eigenes Protokoll");
            if (result.MissingCount > 0)
                parts.Add($"{result.MissingCount}× fehlt");

            StatusMessage = parts.Count == 0
                ? "Keine Leitungen zugeordnet — nichts zu sammeln."
                : "Beilagen: " + string.Join(", ", parts) + ".";

            if (result.Warnings.Count > 0)
            {
                _dialogs.Warn(
                    StatusMessage + "\n\n" + string.Join("\n", result.Warnings.Take(15)),
                    "Beilagen");
            }
            else
            {
                _toasts.Success(StatusMessage);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AssemblePdfAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        IsBusy = true;
        try
        {
            var request = BuildRequest(root, Selected.Definition);

            // "Alles" bedeutet wirklich alles: Vor jeder Zusammenführung
            // werden die Originalprotokolle aller aktuell ausgewählten
            // Haltungen und Schächte neu gesammelt.
            var collected = await _attachments.CollectAsync(request);
            var missingSelections = request.Snapshot.MissingHoldingIds.Count
                + request.Snapshot.MissingShaftNumbers.Count;
            var missing = collected.MissingCount + missingSelections;

            if (missing > 0)
            {
                StatusMessage = missing == 1
                    ? "Gesamt-PDF nicht erstellt: Ein ausgewähltes Protokoll fehlt."
                    : $"Gesamt-PDF nicht erstellt: {missing} ausgewählte Protokolle fehlen.";

                var details = collected.Warnings
                    .Concat(request.Snapshot.MissingHoldingIds.Select(id =>
                        $"Haltung mit Kennung '{id}' ist nicht mehr im Projekt."))
                    .Concat(request.Snapshot.MissingShaftNumbers.Select(number =>
                        $"Schacht '{number}' ist nicht mehr im Projekt."))
                    .Take(15)
                    .ToList();
                _dialogs.Warn(
                    details.Count == 0
                        ? StatusMessage
                        : StatusMessage + "\n\n" + string.Join("\n", details),
                    "Gesamt-PDF");
                return;
            }

            var result = await _pdfAssembly.AssembleAsync(request.TargetFolder);

            StatusMessage = result.Message;

            if (!result.Success)
            {
                _dialogs.Warn(result.Message, "Gesamt-PDF");
                return;
            }

            if (collected.Warnings.Count > 0)
            {
                _dialogs.Warn(
                    result.Message + "\n\n" + string.Join("\n", collected.Warnings.Take(15)),
                    "Gesamt-PDF");
            }

            _toasts.Success(result.Message);

            if (_dialogs.Confirm(result.Message + "\n\nPDF jetzt öffnen?", "Gesamt-PDF"))
                _shellOpen.TryOpen(result.FilePath!, out _);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenFolder()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var folder = ResolveDossierFolder(root, Selected.Definition);
        Directory.CreateDirectory(Path.Combine(
            folder, DossierFolderPlanner.AttachmentFolderName));

        _explorerReveal.TryReveal(folder, out _);
    }

    /// <summary>
    /// Oeffnet die ausgelieferte Word-Vorlage. Sie ist eine von Hand gestaltete
    /// Datei und wird nicht aus Code erzeugt — ein "Zuruecksetzen" gibt es
    /// deshalb nicht mehr; es haette die Vorlage nur zerstoert.
    /// </summary>
    private Task OpenTemplateAsync()
    {
        var path = DossierWordTemplateExportService.DefaultTemplatePath();

        if (!File.Exists(path))
        {
            StatusMessage = "Die Word-Vorlage fehlt: " + path;
            _dialogs.Error(StatusMessage, "Word-Vorlage");
            return Task.CompletedTask;
        }

        if (!_shellOpen.TryOpen(path, out var fehler))
        {
            StatusMessage = "Die Vorlage konnte nicht geöffnet werden: " + fehler;
            _dialogs.Error(StatusMessage, "Word-Vorlage");
            return Task.CompletedTask;
        }

        StatusMessage = "Word-Vorlage geöffnet.";
        return Task.CompletedTask;
    }

    /// <summary>
    /// Zeigt das Dossier Seite fuer Seite und laesst die Felder dieser Seite
    /// direkt daneben ausfuellen. Uebernommen wird nur auf ausdruecklichen
    /// Wunsch — das Fenster arbeitet auf einer Kopie.
    /// </summary>
    private async Task PreviewAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var vorlage = DossierWordTemplateExportService.DefaultTemplatePath();
        if (!File.Exists(vorlage))
        {
            StatusMessage = "Die Word-Vorlage fehlt: " + vorlage;
            _dialogs.Error(StatusMessage, "Vorschau");
            return;
        }

        var definition = Selected.Definition;

        DossierPreviewChoice? ergebnis;
        try
        {
            ergebnis = _dialogWindows.Preview(BuildRequest(root, definition), vorlage);
        }
        catch (Exception ex)
        {
            StatusMessage = "Die Vorschau konnte nicht geöffnet werden: " + ex.Message;
            _dialogs.Error(StatusMessage, "Vorschau");
            return;
        }

        if (ergebnis is null)
        {
            StatusMessage = "Vorschau geschlossen, nichts übernommen.";
            return;
        }

        using var uebernahme = ergebnis;

        var stelle = _document.Dossiers.FindIndex(d => d.Id == definition.Id);
        if (stelle < 0)
        {
            var rollback = uebernahme.RollbackPlanPublication();
            StatusMessage = "Das Dossier ist zwischenzeitlich verschwunden — nichts übernommen.";
            if (!rollback.Success)
                StatusMessage += " " + rollback.Error;
            return;
        }

        // Die Vorschau hat auf Kopien gearbeitet. Zurueckgeschrieben wird an
        // genau die Stelle, von der die Kopie stammt — und der bisherige Stand
        // wird gemerkt: scheitert das Speichern, stuende sonst im Arbeitsspeicher
        // etwas anderes als in der Datei.
        var vorherigesGebiet = _document.Area;
        var vorherigesDossier = _document.Dossiers[stelle];

        _document.Area = uebernahme.Area;
        _document.Dossiers[stelle] = uebernahme.Dossier;

        if (!await SaveDocumentAsync(root))
        {
            _document.Area = vorherigesGebiet;
            _document.Dossiers[stelle] = vorherigesDossier;
            var rollback = uebernahme.RollbackPlanPublication();
            StatusMessage = "Nicht gespeichert — die Angaben bleiben wie vorher.";
            if (!rollback.Success)
                StatusMessage += " " + rollback.Error;
            return;
        }

        uebernahme.AcceptPlanPublication();
        await ReloadAsync();
        StatusMessage = "Angaben aus der Vorschau übernommen.";
    }

    // ── Hilfen ────────────────────────────────────────────────────────────

    /// <summary>
    /// Der Auftrag fuer Word, Vorschau, Beilagen und PDF.
    ///
    /// Der Stand kommt bewusst aus demselben <see cref="BuildSnapshot"/> wie
    /// das Cockpit. Frueher rechnete diese Stelle ihren eigenen Stand — und
    /// gab dabei die Schacht-Kostendatei nicht mit. Das Cockpit zeigte dann
    /// Kosten, die im Brief an den Eigentuemer fehlten.
    /// </summary>
    private DossierExportRequest BuildRequest(string root, DossierDefinition definition)
        => new(
            _getProject(),
            root,
            _document.Area,
            definition,
            BuildSnapshot(definition),
            ResolveDossierFolder(root, definition));

    private static string ResolveDossierFolder(string root, DossierDefinition definition)
    {
        var folderName = string.IsNullOrWhiteSpace(definition.FolderName)
            ? DossierFolderPlanner.PlanFolderName(definition.Name, _ => false)
            : definition.FolderName;

        return DossierFolderPlanner.ResolveDossierFolder(root, folderName);
    }

    private bool EnsureProject(out string root)
    {
        root = _getProjectFolder() ?? "";

        if (string.IsNullOrWhiteSpace(root))
        {
            _dialogs.Warn(
                "Dossiers gehören zu einem Projekt. Bitte zuerst ein Projekt öffnen "
                + "oder speichern.",
                "Kein Projekt");
            return false;
        }

        if (!_loaded)
        {
            _dialogs.Warn(
                "Die Dossier-Datei konnte nicht gelesen werden. Es wird nichts "
                + "gespeichert, damit nichts überschrieben wird.\n\n" + StatusMessage,
                "Dossiers");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Speichert den ganzen Dossierbestand auf Zuruf.
    ///
    /// Jede Aktion speichert bereits selbst. Dieser Weg ist trotzdem nicht
    /// ueberfluessig: Er sagt mit Zeitstempel, DASS der Stand auf der Platte
    /// liegt. Wer eine Stunde an einem Dossier gearbeitet hat, soll sich das
    /// nicht aus dem Ausbleiben einer Fehlermeldung erschliessen muessen.
    ///
    /// Bei unlesbarer Dossierdatei sperrt EnsureProject den Weg, damit ein
    /// unvollstaendig geladener Stand die gute Datei nicht ueberschreibt.
    /// </summary>
    private async Task SaveNowAsync()
    {
        if (!EnsureProject(out var root))
            return;

        if (!await SaveDocumentAsync(root))
            return;

        StatusMessage = BuildSaveConfirmation(_document.Dossiers.Count, DateTime.Now);
    }

    /// <summary>
    /// Die Rueckmeldung des Speicherns. Sie nennt Anzahl und Uhrzeit, weil
    /// genau das die Frage beantwortet, die zum Knopf gefuehrt hat: Ist mein
    /// Stand jetzt auf der Platte?
    /// </summary>
    public static string BuildSaveConfirmation(int count, DateTime when)
    {
        var uhrzeit = when.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);

        return count == 1
            ? $"1 Dossier gespeichert um {uhrzeit} Uhr."
            : $"{count} Dossiers gespeichert um {uhrzeit} Uhr.";
    }

    /// <summary>
    /// Fuehrt das gewaehlte Dossier nach.
    ///
    /// Gefragt wird das PROJEKT, nicht der Kanton: gesucht wird, was seit dem
    /// Anlegen aufgenommen wurde. Deshalb kostet das keine Abfrage und geht
    /// auch ohne Netz.
    /// </summary>
    private async Task RefreshDossierAsync()
    {
        if (Selected is null || !EnsureProject(out var root))
            return;

        var definition = Selected.Definition;

        var vorschlag = DossierRefreshUseCase.Propose(
            definition, HaltungsIdsNachName(), ProjektSchachtnummern());

        if (!vorschlag.HasAnything)
        {
            StatusMessage = "Nichts Neues — das Projekt kennt zu dieser "
                + "Liegenschaft nichts, was nicht schon im Dossier steht.";
            return;
        }

        var auswahl = _dialogWindows.Refresh(definition.Name, vorschlag);
        if (auswahl is null)
            return;

        // Der Stand VOR der Aenderung, damit ein misslungenes Speichern nichts
        // Halbes stehen laesst.
        var vorherLeitungen = new List<Guid>(definition.HoldingIds);
        var vorherSchaechte = new List<string>(definition.ShaftNumbers);
        var vorherAbgelehnteLeitungen = new List<Guid>(definition.DismissedHoldingIds);
        var vorherAbgelehnteSchaechte = new List<string>(definition.DismissedShaftNumbers);

        DossierRefreshUseCase.Apply(
            definition, auswahl.Holdings, auswahl.Shafts, vorschlag);

        definition.ModifiedAtUtc = DateTime.UtcNow;

        if (!await SaveDocumentAsync(root))
        {
            definition.HoldingIds = vorherLeitungen;
            definition.ShaftNumbers = vorherSchaechte;
            definition.DismissedHoldingIds = vorherAbgelehnteLeitungen;
            definition.DismissedShaftNumbers = vorherAbgelehnteSchaechte;
            return;
        }

        RefreshDetail();

        StatusMessage = Nachgefuehrt(auswahl.Holdings.Count, auswahl.Shafts.Count);
    }

    /// <summary>Die Rueckmeldung des Nachfuehrens.</summary>
    public static string Nachgefuehrt(int leitungen, int schaechte)
    {
        if (leitungen == 0 && schaechte == 0)
            return "Nichts übernommen.";

        var teile = new List<string>();

        if (leitungen > 0)
            teile.Add(leitungen == 1 ? "1 Leitung" : leitungen + " Leitungen");

        if (schaechte > 0)
            teile.Add(schaechte == 1 ? "1 Schacht" : schaechte + " Schächte");

        return string.Join(" und ", teile) + " ergänzt.";
    }

    /// <summary>
    /// Die Leitungen des Hauptprojekts nach Namen. Dieselbe Regel fuer jeden
    /// Weg, damit nicht einer etwas findet und der andere nicht.
    /// </summary>
    private IReadOnlyDictionary<string, Guid> HaltungsIdsNachName()
    {
        var idsByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in _getProject().Data)
        {
            var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? string.Empty).Trim();
            if (name.Length > 0)
                idsByName[name] = record.Id;
        }

        return idsByName;
    }

    /// <summary>
    /// Die Schaechte, die das Hauptprojekt wirklich fuehrt. Einzelanlage und
    /// Stapel lesen dieselbe Liste — zwei Kopien derselben Abfrage waren in
    /// diesem Programm schon einmal der Grund, dass ein Weg etwas fand und
    /// der andere nicht.
    /// </summary>
    private IReadOnlyList<string> ProjektSchachtnummern()
        => DossierShaftNumberPolicy.NumbersOf(_getProject());

    private async Task<bool> SaveDocumentAsync(string root)
    {
        try
        {
            await _store.SaveAsync(root, _document);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Speichern fehlgeschlagen: " + ex.Message;
            _dialogs.Error(StatusMessage, "Dossiers");
            return false;
        }
    }
}
