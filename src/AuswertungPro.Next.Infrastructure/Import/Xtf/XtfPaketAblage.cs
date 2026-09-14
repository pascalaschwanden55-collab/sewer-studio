using System.Globalization;
using System.IO.Compression;
using System.Text;
using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Legt den Paketordner an und packt ihn am Schluss. Ueberschreibt nie etwas: Sowohl der
/// Ordner als auch die ZIP bekommen bei Namensgleichheit einen freien Namen.
/// <see cref="Verwirf"/> entfernt ausschliesslich Ordner, die diese Instanz selbst angelegt hat.
/// </summary>
public sealed class XtfPaketAblage : IXtfPaketAblage
{
    public const string AenderungenOrdnername = "1 Aenderungen";
    public const string VollstaendigOrdnername = "2 Vollstaendig";
    public const string Liesmichname = "LIESMICH.txt";
    public const string Berichtname = "Bericht.txt";

    private readonly HashSet<string> _selbstAngelegt = new(StringComparer.OrdinalIgnoreCase);

    public XtfPaketOrt Beginne(string zielordner, string projektname)
    {
        if (string.IsNullOrWhiteSpace(zielordner)) throw new InvalidOperationException("Der Zielordner fehlt.");
        var basis = Path.Combine(Path.GetFullPath(zielordner),
            $"GEONIS-Paket_{Dateiname(projektname)}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}");
        var ordner = FreierPfad(basis);
        Directory.CreateDirectory(ordner);
        _selbstAngelegt.Add(ordner);
        var ort = new XtfPaketOrt(ordner, Path.Combine(ordner, AenderungenOrdnername), Path.Combine(ordner, VollstaendigOrdnername));
        Directory.CreateDirectory(ort.AenderungenOrdner);
        Directory.CreateDirectory(ort.VollstaendigOrdner);
        return ort;
    }

    public string Schliesse(XtfPaketOrt ort, XtfPaketInhalte inhalte)
    {
        ArgumentNullException.ThrowIfNull(ort);
        ArgumentNullException.ThrowIfNull(inhalte);
        File.WriteAllText(Path.Combine(ort.Paketordner, Liesmichname), inhalte.Liesmich ?? "", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(ort.AenderungenOrdner, Berichtname), inhalte.AenderungsBericht ?? "", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(ort.VollstaendigOrdner, Berichtname), inhalte.VollstaendigerBericht ?? "", new UTF8Encoding(false));
        // Die ZIP liegt neben dem Paketordner, damit sie sich nicht selbst einpackt.
        var zip = FreierPfad(ort.Paketordner + ".zip");
        ZipFile.CreateFromDirectory(ort.Paketordner, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
        _selbstAngelegt.Remove(ort.Paketordner);
        return zip;
    }

    public void Verwirf(XtfPaketOrt ort)
    {
        ArgumentNullException.ThrowIfNull(ort);
        if (!_selbstAngelegt.Remove(ort.Paketordner)) return;
        try { if (Directory.Exists(ort.Paketordner)) Directory.Delete(ort.Paketordner, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ein gesperrter Rest darf den eigentlichen Exportfehler nicht verdecken; der
            // Aufrufer meldet ihn. Der Ordner bleibt dann sichtbar liegen.
        }
    }

    private static string Dateiname(string name)
    {
        var sauber = new string((name ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        return sauber.Length == 0 ? "Projekt" : sauber.Length <= 60 ? sauber : sauber[..60];
    }

    /// <summary>Haengt _2, _3 an, bis der Pfad frei ist. Nie eine bestehende Ablage ueberschreiben.</summary>
    private static string FreierPfad(string wunsch)
    {
        if (!Directory.Exists(wunsch) && !File.Exists(wunsch)) return wunsch;
        var ordner = Path.GetDirectoryName(wunsch) ?? "";
        var name = Path.GetFileNameWithoutExtension(wunsch);
        var endung = Path.GetExtension(wunsch);
        for (var i = 2; i < 1000; i++)
        {
            var kandidat = Path.Combine(ordner, $"{name}_{i}{endung}");
            if (!Directory.Exists(kandidat) && !File.Exists(kandidat)) return kandidat;
        }

        throw new IOException($"Kein freier Name für {wunsch} gefunden.");
    }
}
