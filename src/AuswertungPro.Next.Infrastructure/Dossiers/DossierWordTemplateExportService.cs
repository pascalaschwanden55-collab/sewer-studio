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

    private readonly Func<string> _resolveTemplatePath;

    public DossierWordTemplateExportService(Func<string>? resolveTemplatePath = null)
        => _resolveTemplatePath = resolveTemplatePath ?? DefaultTemplatePath;

    /// <summary>Standardpfad der ausgelieferten Vorlage.</summary>
    public static string DefaultTemplatePath()
        => Path.Combine(
            AppContext.BaseDirectory,
            "Export_Vorlage",
            DossierWordTemplateBuilder.TemplateFileName);

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

            try
            {
                using (var document = WordprocessingDocument.Open(tempPath, isEditable: true))
                {
                    DocxPlaceholderFiller.FillRepeatingRows(
                        document,
                        "Haltungen",
                        BuildHoldingRows(request.Snapshot),
                        "Keine Leitungen zugeordnet");

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

            return Task.FromResult(new DossierWordExportResult(
                true,
                targetPath,
                $"Word-Datei erstellt: {Path.GetFileName(targetPath)}"));
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
            ["Logo_Hinweis"] = string.IsNullOrWhiteSpace(request.Area.LogoPath)
                ? "[Logo hier einfügen]"
                : string.Empty,
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
            ["Eigentuemer_Block"] = JoinLines(d.OwnerName, d.OwnerAddress),
            ["Eigentuemer_Detail"] = BuildOwnerDetail(d),
            ["Kontakt"] = d.ContactName,
            ["Telefon"] = d.ContactPhone,
            ["EMail"] = d.ContactMail,
            ["Objektbewohner"] = d.Occupancy,
            ["Datum"] = today.ToString("dd.MM.yyyy", Ch),
            ["Datum_Lang"] = today.ToString("dd. MMMM yyyy", Ch),
            ["Revision"] = d.Revision,
            ["Autor"] = Environment.UserName,
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
                : string.Empty
        };
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
