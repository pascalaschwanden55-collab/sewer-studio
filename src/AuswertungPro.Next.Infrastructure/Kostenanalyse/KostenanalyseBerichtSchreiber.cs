using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Kostenanalyse;

namespace AuswertungPro.Next.Infrastructure.Kostenanalyse;

/// <summary>
/// Schreibt das Messergebnis als Bericht mit SHA-256 daneben — wie die uebrigen
/// Messberichte des Projekts. Ein bestehender Bericht wird nie ueberschrieben: Eine
/// zweite Messung desselben Zeitpunkts waere sonst still verschwunden.
/// </summary>
public static class KostenanalyseBerichtSchreiber
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Schreibe(string wurzel, KostenanalyseMessErgebnis ergebnis, DateTime zeitpunktUtc)
    {
        ArgumentNullException.ThrowIfNull(ergebnis);
        if (string.IsNullOrWhiteSpace(wurzel))
            throw new ArgumentException("Wurzel fehlt.", nameof(wurzel));

        var ordner = Path.Combine(wurzel, "kostenanalyse", "berichte");
        Directory.CreateDirectory(ordner);

        var name = $"kostenanalyse_rueckblick_{zeitpunktUtc:yyyyMMdd_HHmmss}.json";
        var pfad = Path.Combine(ordner, name);

        if (File.Exists(pfad))
            throw new IOException($"Bericht existiert bereits und wird nicht ueberschrieben: {pfad}");

        var dokument = new
        {
            erzeugtUtc = zeitpunktUtc.ToString("O", CultureInfo.InvariantCulture),
            art = "rueckblick_leave_one_out",
            hinweis = "Standortbestimmung, keine Freigabe. Misst nur den vorhandenen Bestand.",
            gesamt = ergebnis.Gesamt,
            mitVorschlag = ergebnis.MitVorschlag,
            enthalten = ergebnis.Enthalten,
            abdeckung = Math.Round(ergebnis.Abdeckung, 4),
            positionenRichtig = ergebnis.PositionenRichtig,
            positionenZuviel = ergebnis.PositionenZuviel,
            positionenFehlend = ergebnis.PositionenFehlend,
            // Die geschenkte Zahl oben taeuscht: Routinepositionen kommen in fast
            // jeder Haltung vor. Massgeblich ist der Block darunter.
            routinePositionen = ergebnis.RoutinePositionen,
            entscheidendePositionen = ergebnis.EntscheidendePositionen,
            entscheidend = new
            {
                richtig = ergebnis.EntscheidendRichtig,
                zuviel = ergebnis.EntscheidendZuviel,
                fehlend = ergebnis.EntscheidendFehlend,
                genauigkeit = Math.Round(ergebnis.EntscheidendGenauigkeit, 4),
                vollstaendigkeit = Math.Round(ergebnis.EntscheidendVollstaendigkeit, 4)
            },
            gegenprobeStandardpaket = new
            {
                hinweis = "Ohne jede Aehnlichkeitssuche - immer dasselbe Paket.",
                richtig = ergebnis.BasisRichtig,
                zuviel = ergebnis.BasisZuviel,
                fehlend = ergebnis.BasisFehlend,
                genauigkeit = Math.Round(ergebnis.BasisGenauigkeit, 4),
                vollstaendigkeit = Math.Round(ergebnis.BasisVollstaendigkeit, 4)
            },
            schwellen = new
            {
                mindestNachbarn = KostenVorschlagPolicy.MindestNachbarn,
                maximalNachbarn = KostenVorschlagPolicy.MaximalNachbarn,
                mindestBogenFaelle = KostenVorschlagPolicy.MindestBogenFaelle,
                routineSchwelle = KostenanalyseMessung.RoutineSchwelle
            }
        };

        var inhalt = JsonSerializer.Serialize(dokument, Optionen);
        File.WriteAllText(pfad, inhalt);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inhalt))).ToLowerInvariant();
        File.WriteAllText(pfad + ".sha256", hash);

        return pfad;
    }
}
