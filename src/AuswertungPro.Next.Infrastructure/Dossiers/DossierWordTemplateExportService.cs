using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DocumentFormat.OpenXml.Packaging;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Erzeugt die Word-Datei eines Eigentuemerdossiers aus der Vorlage
/// "Export_Vorlage\Eigentuemerdossier.docx".
///
/// Eine bereits vorhandene Datei wird nie ueberschrieben — Pascal ergaenzt in
/// dieser Datei von Hand, ein stilles Ueberschreiben wuerde seine Arbeit
/// vernichten. Stattdessen entsteht ein freier Name.
/// </summary>
public sealed class DossierWordTemplateExportService : IDossierWordExportService
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    /// <summary>Das feste Firmenlogo, ausgeliefert neben der Word-Vorlage.</summary>
    public const string LogoFileName = "Dossier_Logo.png";

    /// <summary>Das feste Wappen, ausgeliefert neben der Word-Vorlage.</summary>
    public const string CoatOfArmsFileName = "Dossier_Wappen.png";

    private readonly Func<string> _resolveTemplatePath;

    public DossierWordTemplateExportService(Func<string>? resolveTemplatePath = null)
        => _resolveTemplatePath = resolveTemplatePath ?? DefaultTemplatePath;

    /// <summary>Standardpfad der ausgelieferten Vorlage.</summary>
    public static string DefaultTemplatePath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Export_Vorlage",
            DossierWordTemplate.TemplateFileName);

    public Task<DossierWordExportResult> ExportAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var templatePath = _resolveTemplatePath();
        if (!File.Exists(templatePath))
        {
            return Task.FromResult(new DossierWordExportResult(
                false,
                null,
                $"Die Word-Vorlage fehlt: '{templatePath}'. "
                + "Sie lässt sich in den Einstellungen des Dossier-Bereichs neu erzeugen."));
        }

        try
        {
            var guard = new ProjectWritePathGuard(request.ProjectRoot);
            var folder = guard.EnsureSafeDirectoryTarget(request.TargetFolder);
            Directory.CreateDirectory(folder);

            var fileName = DossierFolderPlanner.PlanFreeFileName(
                DossierFolderPlanner.WordFileName,
                candidate => File.Exists(Path.Combine(folder, candidate)));

            var targetPath = guard.EnsureSafeFileTarget(Path.Combine(folder, fileName));

            // Erst neben das Ziel schreiben, dann veroeffentlichen: ein Abbruch
            // mitten im Fuellen darf keine halbe Datei hinterlassen.
            var tempPath = targetPath + ".tmp";
            File.Copy(templatePath, tempPath, overwrite: true);

            IReadOnlyList<string> missingImages;
            try
            {
                using (var document = WordprocessingDocument.Open(tempPath, isEditable: true))
                {
                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Haltungen",
                        BuildHoldingRows(request.Snapshot),
                        "Keine Leitungen zugeordnet");

                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Eigentuemer",
                        BuildOwnerRows(request.Dossier),
                        "Keine Eigentümerangaben erfasst");

                    // Bilder VOR dem Textfueller: sonst wuerde der Textfueller
                    // "{{@Logo}}" als unbekannten Textplatzhalter leeren und das
                    // Bild fehlte im fertigen Dossier ohne jede Meldung.
                    missingImages = DocxImagePlaceholderFiller.Fill(
                        document, BuildImagePlacements(request, templatePath));

                    DocxPlaceholderFiller.Fill(document, BuildValues(request));
                    document.MainDocumentPart?.Document?.Save();
                }

                File.Move(tempPath, targetPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            var message = $"Word-Datei erstellt: {Path.GetFileName(targetPath)}";
            var hint = BuildMissingImagesHint(missingImages);
            if (hint.Length > 0)
                message += "  (Hinweis: " + hint + ")";

            return Task.FromResult(new DossierWordExportResult(true, targetPath, message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DossierWordExportResult(
                false, null, $"Die Word-Datei konnte nicht erstellt werden: {ex.Message}"));
        }
    }

    /// <summary>Baut die Platzhalterwerte. Oeffentlich, damit sie testbar sind.</summary>
    public static Dictionary<string, string> BuildValues(DossierExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var d = request.Dossier;
        var resolved = DossierFieldResolver.Resolve(request.Area, d);
        var snapshot = request.Snapshot;
        var today = DateTime.Now;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gebietstitel"] = resolved.AreaTitle,
            ["Parzellen"] = d.ParcelNumbers,
            ["Parzellen_Zeile"] = string.IsNullOrWhiteSpace(d.ParcelNumbers)
                ? string.Empty
                : "Parzellen " + d.ParcelNumbers,
            ["Hausnummern"] = d.HouseNumbers,
            ["Adresse_Zeile"] = BuildAddressLine(d),
            ["Adresse"] = d.Address,
            ["PLZ"] = d.PostalCode,
            ["Ort"] = d.Town,
            ["Eigentuemer"] = d.OwnerName,
            ["Eigentuemer_Block"] = BuildCoverOwnerBlock(d),
            ["Eigentuemer_Detail"] = BuildOwnerDetail(d),
            ["Kontakt"] = d.ContactName,
            ["Telefon"] = d.ContactPhone,
            ["EMail"] = d.ContactMail,
            ["Objektbewohner"] = d.Occupancy,
            ["Datum"] = today.ToString("dd.MM.yyyy", Ch),
            ["Datum_Lang"] = today.ToString("dd. MMMM yyyy", Ch),
            ["Revision"] = d.Revision,
            // Kein Rueckfall auf Environment.UserName: "lieber leer als
            // falsch" — der Windows-Benutzername gehoert nicht in ein
            // Dokument fuer den Eigentuemer.
            ["Autoren"] = string.IsNullOrWhiteSpace(request.Area.Authors)
                ? string.Empty
                : request.Area.Authors.Trim(),
            ["Dossier_Name"] = d.Name,

            ["Ausfuehrungstermin"] = resolved.ExecutionDate,
            ["Ansprechpartner"] = resolved.ContactPerson,
            ["Unternehmer"] = resolved.Contractor,
            ["Bauleitung"] = resolved.SiteManagement,
            ["Behinderungen"] = resolved.Obstructions,
            ["Bauvorgang"] = d.ConstructionProcess,
            ["Hausanschluss"] = resolved.HouseConnectionText,
            ["Meteorwasser"] = resolved.StormWaterText,
            ["Bemerkungen"] = d.Remarks,
            ["Beilagen"] = d.Attachments,
            ["Rueckmeldung"] = BuildResponseText(resolved.ResponseDeadline),
            ["Fusszeile"] = resolved.FooterLine,

            ["Anzahl_Haltungen"] = snapshot.HoldingCount.ToString(CultureInfo.InvariantCulture),
            ["Laenge_Total"] = FormatLength(snapshot.LengthTotal),
            ["Kosten_Total"] = FormatChf(snapshot.NetCostTotal),
            ["Kosten_Hinweis"] = snapshot.NetCostTotal > 0m
                ? "Kostenangaben ohne MWST, Stand " + today.ToString("dd.MM.yyyy", Ch) + "."
                : string.Empty,
            ["Haltungen_Text"] = BuildHoldingsText(snapshot),
            ["Haltungen_Summe"] = BuildHoldingsSummary(snapshot, today)
        };
    }

    /// <summary>
    /// Die ausgewaehlten Leitungen als Textblock — eine Zeile je Leitung.
    ///
    /// Die Vorlage kann sie stattdessen auch als Tabelle fuehren; dafuer gibt es
    /// die Wiederholzeile <c>{{#Haltungen}}</c>. Der Textblock ist der Weg, der
    /// ohne Tabellenbau in Word auskommt.
    /// </summary>
    private static string BuildHoldingsText(DossierSnapshot snapshot)
    {
        if (snapshot.Holdings.Count == 0)
            return "Keine Leitungen zugeordnet.";

        var zeilen = new List<string>();
        foreach (var holding in snapshot.Holdings)
        {
            var teile = new List<string> { holding.HoldingName };

            if (!string.IsNullOrWhiteSpace(holding.Street))
                teile.Add(holding.Street);

            var laenge = FormatLength(holding.LengthMeters);
            if (!string.IsNullOrWhiteSpace(laenge))
                teile.Add(laenge);

            var zustand = FormatCondition(holding.ConditionClass);
            if (!string.IsNullOrWhiteSpace(zustand))
                teile.Add(zustand);

            if (!string.IsNullOrWhiteSpace(holding.Measures))
                teile.Add(holding.Measures);

            if (holding.NetCost > 0m)
                teile.Add(FormatChf(holding.NetCost));

            zeilen.Add(string.Join(" · ", teile));
        }

        return string.Join(Environment.NewLine, zeilen);
    }

    private static string BuildHoldingsSummary(DossierSnapshot snapshot, DateTime today)
    {
        if (snapshot.Holdings.Count == 0)
            return string.Empty;

        var teile = new List<string>
        {
            snapshot.HoldingCount == 1 ? "1 Leitung" : snapshot.HoldingCount + " Leitungen"
        };

        var laenge = FormatLength(snapshot.LengthTotal);
        if (!string.IsNullOrWhiteSpace(laenge))
            teile.Add("Gesamtlänge " + laenge);

        if (snapshot.NetCostTotal > 0m)
        {
            teile.Add("Kostenschätzung " + FormatChf(snapshot.NetCostTotal)
                + " (ohne MWST, Stand " + today.ToString("dd.MM.yyyy", Ch) + ")");
        }

        return string.Join(" · ", teile);
    }

    private static List<IReadOnlyDictionary<string, string>> BuildHoldingRows(
        DossierSnapshot snapshot)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var holding in snapshot.Holdings)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Haltung"] = holding.HoldingName,
                ["Strasse"] = holding.Street,
                ["Laenge"] = FormatLength(holding.LengthMeters),
                ["Zustand"] = FormatCondition(holding.ConditionClass),
                ["Massnahme"] = holding.Measures,
                ["Kosten"] = holding.NetCost > 0m ? FormatChf(holding.NetCost) : "—"
            });
        }

        return rows;
    }

    /// <summary>
    /// Die Zeilen der Tabelle "Eigentumsverhaeltnisse". Oeffentlich, damit sie
    /// testbar sind.
    /// </summary>
    public static List<IReadOnlyDictionary<string, string>> BuildOwnerRows(
        DossierDefinition dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var rows = new List<IReadOnlyDictionary<string, string>>();

        foreach (var owner in dossier.Owners)
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Haus_Nr"] = Clean(owner.HouseNumber),
                ["Pz_Nr"] = Clean(owner.ParcelNumber),
                ["Eigentuemer_Zelle"] = BuildOwnerCell(owner)
            });
        }

        return rows;
    }

    /// <summary>
    /// Der mehrzeilige Inhalt der Eigentuemerzelle — dieselbe Aufteilung wie im
    /// Vorbild. Leere Angaben erzeugen keine leere Beschriftungszeile.
    /// </summary>
    private static string BuildOwnerCell(DossierOwnerRow owner)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(owner.Name))
            parts.Add(owner.Name.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Phone))
            parts.Add("Tel.: " + owner.Phone.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Mail))
            parts.Add("Mail: " + owner.Mail.Trim());

        if (!string.IsNullOrWhiteSpace(owner.Occupancy))
            parts.Add("Objektbewohner: " + owner.Occupancy.Trim());

        return string.Join("\n", parts);
    }

    /// <summary>
    /// Auf dem Deckblatt stehen die klassischen Felder "Eigentuemer"/"Adresse
    /// des Eigentuemers", sofern eines von beiden gefuellt ist — sonst gehen
    /// sie verloren, sobald die Tabellenzeilen einen gekuerzten Namen tragen.
    /// Erst wenn beide leer sind, gelten stattdessen die Namen aller
    /// Eigentuemerzeilen untereinander.
    /// </summary>
    private static string BuildCoverOwnerBlock(DossierDefinition dossier)
    {
        var legacy = JoinLines(dossier.OwnerName, dossier.OwnerAddress);
        if (legacy.Length > 0)
            return legacy;

        var names = dossier.Owners
            .Select(owner => owner.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Split('\n')[0].Trim())
            .ToList();

        return string.Join("\n", names);
    }

    /// <summary>
    /// Logo und Wappen liegen fest neben der Word-Vorlage; der Uebersichtsplan
    /// gehoert zur einzelnen Liegenschaft. Ein relativer Planpfad wird am
    /// Projektordner aufgeloest.
    /// </summary>
    private static List<DocxImagePlacement> BuildImagePlacements(
        DossierExportRequest request,
        string templatePath)
    {
        var placements = new List<DocxImagePlacement>();
        var templateFolder = Path.GetDirectoryName(templatePath);

        if (!string.IsNullOrWhiteSpace(templateFolder))
        {
            placements.Add(new DocxImagePlacement(
                "Logo", Path.Combine(templateFolder, LogoFileName), MaxWidthCm: 4.5));
            placements.Add(new DocxImagePlacement(
                "Wappen", Path.Combine(templateFolder, CoatOfArmsFileName), MaxWidthCm: 2.0));
        }

        var plan = ResolvePlanPath(request);
        if (plan is not null)
            placements.Add(new DocxImagePlacement("Uebersichtsplan", plan, MaxWidthCm: 15.0));

        return placements;
    }

    /// <summary>
    /// Baut den Hinweistext fuer nicht eingesetzte Bilder — in Klartext, nicht
    /// mit den technischen Platzhalternamen. Pascal soll das VOR dem Versand
    /// merken, nicht erst beim Eigentuemer.
    /// </summary>
    private static string BuildMissingImagesHint(IReadOnlyList<string> missingPlaceholders)
        => string.Join(" ", missingPlaceholders.Select(DescribeMissingImage));

    private static string DescribeMissingImage(string placeholderName) => placeholderName switch
    {
        "Logo" => "Firmenlogo nicht gefunden.",
        "Wappen" => "Wappen nicht gefunden.",
        "Uebersichtsplan" => "Übersichtsplan nicht gefunden – Kapitel 1 bleibt leer.",
        _ => placeholderName + " nicht gefunden."
    };

    private static string? ResolvePlanPath(DossierExportRequest request)
    {
        var configured = request.Dossier.OverviewPlanPath;
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        try
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(request.ProjectRoot, configured);
        }
        catch
        {
            // Ein unsinniger Pfad darf das Dossier nicht verhindern; die Stelle
            // bleibt dann leer.
            return null;
        }
    }

    private static string Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string BuildAddressLine(DossierDefinition d)
    {
        var street = string.Join(
            " ",
            new[] { d.Address, d.HouseNumbers }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var town = string.Join(
            " ",
            new[] { d.PostalCode, d.Town }.Where(p => !string.IsNullOrWhiteSpace(p)));

        return JoinLines(street, town);
    }

    private static string BuildOwnerDetail(DossierDefinition d)
    {
        var parts = new List<string>();

        var owner = JoinInline(d.OwnerName, d.OwnerAddress);
        if (owner.Length > 0)
            parts.Add(owner);

        if (!string.IsNullOrWhiteSpace(d.ContactName))
            parts.Add("Zuständigkeit: " + d.ContactName.Trim());

        if (!string.IsNullOrWhiteSpace(d.ContactPhone))
            parts.Add("Tel.: " + d.ContactPhone.Trim());

        if (!string.IsNullOrWhiteSpace(d.ContactMail))
            parts.Add("E-Mail: " + d.ContactMail.Trim());

        if (!string.IsNullOrWhiteSpace(d.Occupancy))
            parts.Add("Objektbewohner: " + d.Occupancy.Trim());

        return string.Join("\n", parts);
    }

    private static string BuildResponseText(string deadline)
        => string.IsNullOrWhiteSpace(deadline)
            ? "Rückmeldung erbeten."
            : "Rückmeldung bis " + deadline.Trim() + ":";

    private static string JoinLines(params string?[] parts)
        => string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string JoinInline(params string?[] parts)
        => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string FormatChf(decimal value)
        => value.ToString("#,##0.00", Ch);

    private static string FormatLength(double? meters)
        => meters is null or 0d ? "—" : meters.Value.ToString("0.00", Ch) + " m";

    /// <summary>
    /// Zustandsklasse fuer den Eigentuemer lesbar machen. "ohne" heisst, dass
    /// keine Klasse hinterlegt ist — das wird als Strich gezeigt und nicht als
    /// Klasse 0 ausgegeben, die "dringend" bedeuten wuerde.
    /// </summary>
    private static string FormatCondition(string conditionClass) => conditionClass switch
    {
        "0" => "Z0 – sofort",
        "1" => "Z1 – kurzfristig",
        "2" => "Z2 – mittelfristig",
        "3" => "Z3 – langfristig",
        "4" => "Z4 – kein Mangel",
        _ => "—"
    };

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Ein liegen gebliebener .tmp ist harmlos; ein Folgefehler hier
            // wuerde die eigentliche Ursache verdecken.
        }
    }
}
