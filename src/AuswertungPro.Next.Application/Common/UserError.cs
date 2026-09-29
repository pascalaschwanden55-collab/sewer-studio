using System.Net.Http;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Markiert einen bewusst formulierten Text, der Nutzern sicher gezeigt werden darf.</summary>
public sealed class UserFacingException : Exception
{
    public UserFacingException(string userMessage)
        : base(string.IsNullOrWhiteSpace(userMessage)
            ? throw new ArgumentException("Eine Nutzerfehlermeldung darf nicht leer sein.", nameof(userMessage))
            : userMessage.Trim())
    {
    }
}

/// <summary>Uebersetzt technische Ausnahmen in kurze, sichere Nutzerhinweise.</summary>
public static class UserError
{
    /// <summary>
    /// Die drei unteren Schichten, exakt beim Assembly-Namen genannt statt per Praefix: ein
    /// Praefix "AuswertungPro" traefe auch jedes Testprojekt (<c>AuswertungPro.Next.UI.Tests</c>
    /// usw.) und das Werkzeug <c>tools/AuswertungPro.MeasureCatalogCli</c> - beide sind NICHT das
    /// Produkt. Die UI-Assembly heisst zudem nicht "AuswertungPro.Next.UI", sondern "SewerStudio"
    /// (siehe <c>AuswertungPro.Next.UI.csproj</c>, <c>&lt;AssemblyName&gt;</c>) und bleibt hier
    /// bewusst AUSSEN VOR: Ein realer Fall (<c>SettingsPathWorkflow.OpenFolderCore</c>) wirft dort
    /// <c>throw new InvalidOperationException(result.Error ?? "Unbekannter Fehler")</c> - eine
    /// dynamische Weiterreichung, deren <c>result.Error</c> aus <c>FolderOpenService</c> im
    /// Fehlerfall ein rohes <c>ex.Message</c> eines fremden/OS-Fehlers sein kann, kein von uns
    /// verfasster Satz. In der UI-Schicht lassen sich solche durchgereichten Fremdtexte von
    /// echten eigenen Meldungen nicht am Assembly-Namen allein unterscheiden; die drei unteren
    /// Schichten werfen dagegen ausschliesslich fest verfasste deutsche Saetze als
    /// <see cref="InvalidOperationException"/>/<see cref="ArgumentException"/> (siehe die beiden
    /// namentlich geprueften Faelle in CodingSessionService und DocxPlaceholderFiller, beide
    /// Infrastructure).
    /// </summary>
    private static readonly string[] EigeneProduktAssemblies =
    [
        "AuswertungPro.Next.Domain",
        "AuswertungPro.Next.Application",
        "AuswertungPro.Next.Infrastructure"
    ];

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var relevant = Unwrap(exception);

        if (relevant is not UserFacingException
            && IstEigeneVorsaetzlicheMeldung(relevant)
            && OhneFremdtext(relevant) is { } eigeneMeldung)
            return $"{eigeneMeldung} Technische Details stehen im Programmlog.";

        return relevant switch
        {
            UserFacingException => relevant.Message,
            OperationCanceledException =>
                "Der Vorgang wurde abgebrochen.",
            TimeoutException =>
                "Der Vorgang hat zu lange gedauert. Bitte erneut versuchen.",
            UnauthorizedAccessException =>
                "Zugriff wurde verweigert. Bitte Ordnerrechte und Dateischutz prüfen.",
            FileNotFoundException =>
                "Eine benötigte Datei wurde nicht gefunden. Bitte Pfad und Datensicherung prüfen.",
            DirectoryNotFoundException =>
                "Ein benötigter Ordner wurde nicht gefunden. Bitte den Speicherort prüfen.",
            PathTooLongException =>
                "Der Datei- oder Ordnerpfad ist zu lang. Bitte einen kürzeren Speicherort verwenden.",
            IOException =>
                "Eine Datei oder ein Ordner ist momentan nicht verfügbar. Bitte schliessen Sie andere Zugriffe und versuchen Sie es erneut.",
            HttpRequestException =>
                "Ein benötigter lokaler Dienst ist nicht erreichbar. Bitte KI-Dienste und Verbindung prüfen.",
            JsonException or InvalidDataException =>
                "Die gelesenen Daten sind beschädigt oder nicht gültig. Bitte Original oder Datensicherung prüfen.",
            OutOfMemoryException =>
                "Nicht genügend Arbeitsspeicher verfügbar. Bitte andere grosse Vorgänge schliessen und erneut versuchen.",
            NotSupportedException =>
                "Dieser Vorgang oder Dateityp wird nicht unterstützt.",
            _ =>
                "Der Vorgang konnte nicht abgeschlossen werden. Technische Details stehen im Programmlog."
        };
    }

    /// <summary>
    /// Fix-Runde 1 zu Aufgabe 10c2: Fuer einfache Eingabehinweise (Pflichtfeld, Zahlformat,
    /// Listeneintrag schon vorhanden). Eine eigene Meldung erscheint OHNE den Zusatz
    /// "Technische Details stehen im Programmlog." und ohne Stacktrace im Log — es ist kein
    /// technischer Fehler. Alles andere (fremde Ausnahme) laeuft wie <see cref="DescribeAndReport"/>.
    /// </summary>
    public static string DescribeInputHint(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var relevant = Unwrap(exception);
        if (relevant is UserFacingException)
            return relevant.Message;
        if (IstEigeneVorsaetzlicheMeldung(relevant) && OhneFremdtext(relevant) is { } eigeneMeldung)
            return eigeneMeldung;
        return DescribeAndReport(exception, context);
    }

    public static string DescribeAndReport(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var safeContext = string.IsNullOrWhiteSpace(context) ? "Vorgang" : context.Trim();
        BestEffort.ReportWarning($"[{safeContext}] {exception}");
        return Describe(exception);
    }

    /// <summary>
    /// Fix-Runde 1 zu Aufgabe 10c2: Eine eigene Meldung bettet oft den Text der aufgefangenen
    /// Ausnahme ein ("… nicht verfügbar: {ex.Message}"). Ist diese innere Ausnahme KEINE eigene
    /// Meldung (englischer HTTP-/ZIP-/JSON-/SQLite-Text), wird die eigene Meldung an der Stelle
    /// abgeschnitten, an der der Fremdtext beginnt; der deutsche Vorspann bleibt, Trennzeichen am
    /// Ende (": ", " – ", " (") fallen weg. Eine eingebettete EIGENE Meldung (z. B.
    /// TrainingYoloClassMapException in TrainingExportPlanException) bleibt stehen. Der volle Text
    /// samt innerer Ausnahme geht ueber <see cref="DescribeAndReport"/> ins Programmlog.
    /// </summary>
    internal static string? OhneFremdtext(Exception exception)
    {
        var fremdtexte = new List<string>();
        foreach (var innere in InnereAusnahmen(exception))
        {
            if (IstEigeneVorsaetzlicheMeldung(innere) || innere is UserFacingException)
                continue;
            fremdtexte.Add(innere.Message);
        }

        return SchneideFremdtext(exception.Message, fremdtexte);
    }

    /// <summary>
    /// Mindestlaenge eines eingebetteten Fremdtexts, ab der geschnitten wird (Fix-Runde 2).
    /// Kurze innere Meldungen ("Fehler", "Timeout", ein Dateiname) kommen zu leicht zufaellig
    /// schon im deutschen Vorspann vor; ein Schnitt dort wuerde die eigene Meldung zerstoeren.
    /// </summary>
    internal const int FremdtextMindestlaenge = 12;

    private static readonly string[] Trenner = [": ", " – ", " — ", " - ", " (", "; "];

    /// <summary>
    /// Reine Schnittregel zu <see cref="OhneFremdtext"/>. Liefert den deutschen Vorspann vor dem
    /// ersten eingebetteten Fremdtext, den unveraenderten Text, wenn nichts Eingebettetes gefunden
    /// wird, und <c>null</c>, wenn die Meldung MIT dem Fremdtext beginnt — dann ist sie keine
    /// eigene Formulierung, und der Aufrufer faellt auf den Satz fuer den Ausnahmetyp zurueck.
    /// Bevorzugt wird ein Vorkommen direkt hinter einem Trenner («: », « – », « (» …), weil die
    /// eigenen Meldungen den Fremdtext genau so anhaengen; erst ohne ein solches zaehlt das
    /// frueheste Vorkommen.
    /// </summary>
    internal static string? SchneideFremdtext(string text, IEnumerable<string> fremdtexte)
    {
        var schnitt = text.Length;
        foreach (var roh in fremdtexte)
        {
            if (string.IsNullOrWhiteSpace(roh))
                continue;
            var fremd = roh.Trim();
            if (fremd.Length < FremdtextMindestlaenge)
                continue;

            if (text.StartsWith(fremd, StringComparison.Ordinal))
                return null;

            var stelle = StelleHinterTrenner(text, fremd);
            if (stelle < 0)
                stelle = text.IndexOf(fremd, StringComparison.Ordinal);
            if (stelle > 0 && stelle < schnitt)
                schnitt = stelle;
        }

        if (schnitt == text.Length)
            return text;

        var vorspann = text[..schnitt].TrimEnd(' ', ':', ';', ',', '(', '-', '–', '—');
        if (vorspann.Length == 0)
            return null;
        return vorspann.EndsWith('.') || vorspann.EndsWith('!') || vorspann.EndsWith('?')
            ? vorspann
            : vorspann + ".";
    }

    private static int StelleHinterTrenner(string text, string fremd)
    {
        var suche = 0;
        while (suche < text.Length)
        {
            var stelle = text.IndexOf(fremd, suche, StringComparison.Ordinal);
            if (stelle < 0)
                return -1;
            foreach (var trenner in Trenner)
            {
                if (stelle >= trenner.Length
                    && string.CompareOrdinal(text, stelle - trenner.Length, trenner, 0, trenner.Length) == 0)
                    return stelle;
            }
            suche = stelle + 1;
        }
        return -1;
    }

    private static IEnumerable<Exception> InnereAusnahmen(Exception exception)
    {
        var offen = new Stack<Exception>();
        if (exception.InnerException is { } erste)
            offen.Push(erste);
        var gesehen = 0;
        while (offen.Count > 0 && gesehen++ < 32)
        {
            var aktuell = offen.Pop();
            yield return aktuell;
            if (aktuell is AggregateException aggregat)
            {
                foreach (var kind in aggregat.InnerExceptions)
                    offen.Push(kind);
            }
            else if (aktuell.InnerException is { } weiter)
            {
                offen.Push(weiter);
            }
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException { InnerExceptions.Count: 1 } aggregate)
            exception = aggregate.InnerExceptions[0];
        return exception;
    }

    /// <summary>
    /// Erkennt eine im eigenen Code bewusst formulierte deutsche Meldung: eine Ausnahme, deren
    /// LAUFZEITTYP EXAKT <see cref="InvalidOperationException"/> oder <see cref="ArgumentException"/>
    /// ist (nicht per <c>is</c>-Mustervergleich, der auch Unterklassen traefe), geworfen
    /// nachweislich in einer eigenen SewerStudio-Assembly (nicht im .NET-Framework oder in einer
    /// Drittbibliothek). Nur dort ist der Ausnahmetext selbst der Nutzertext.
    /// <c>exception.GetType() == typeof(...)</c> statt <c>is</c> ist hier BEWUSST gewaehlt: Unterklassen
    /// wie <see cref="ArgumentNullException"/>, <see cref="ArgumentOutOfRangeException"/> oder
    /// <see cref="ObjectDisposedException"/> (: <see cref="InvalidOperationException"/>) sind
    /// Programmierfehler-Schutzklauseln mit Framework- oder knapp-englischem Text (z. B.
    /// "Value cannot be null. (Parameter 'x')" oder, real im Code gefunden,
    /// <c>PhotoMeasurementAnglePlanBuilder.cs</c>: "Only LateralCircle and PipeBend are
    /// supported." als <see cref="ArgumentOutOfRangeException"/>) - kein von uns fuer Nutzer
    /// verfasster Satz. Mit blossem <c>is (InvalidOperationException or ArgumentException)</c>
    /// waeren solche Unterklassen faelschlich als "eigene Meldung" durchgereicht worden (real
    /// gefundener Fehler in Fix-Runde 3, hier korrigiert). Anderswo (z. B.
    /// <c>ArgumentNullException.ThrowIfNull</c>, das im Framework wirft, oder ein von einer
    /// fremden Bibliothek geworfener Fehler) bleibt es ohnehin beim generischen Satz.
    /// <see cref="Exception.TargetSite"/> zeigt die Methode, in der tatsaechlich geworfen wurde;
    /// fehlt sie (kein Stacktrace), bleibt die Erkennung fail-safe beim generischen Satz.
    /// </summary>
    private static bool IstEigeneVorsaetzlicheMeldung(Exception exception)
    {
        var typ = exception.GetType();
        if (!IstEigenerMeldungstyp(typ))
            return false;

        var assemblyName = exception.TargetSite?.DeclaringType?.Assembly.GetName().Name;
        return assemblyName is not null
            && Array.IndexOf(EigeneProduktAssemblies, assemblyName) >= 0;
    }

    /// <summary>
    /// Aufgabe 10c2: Neben <see cref="InvalidOperationException"/>/<see cref="ArgumentException"/>
    /// werfen die drei unteren Schichten ihre bewusst formulierten deutschen Saetze auch als
    /// <see cref="IOException"/> (z. B. <c>ProjectPathResolver</c>: "Ohne gespeichertes Projekt
    /// dürfen Dateipfade nicht geändert werden."), <see cref="InvalidDataException"/> und
    /// <see cref="JsonException"/> (Validierung von Register-, Journal- und Manifestdateien) sowie
    /// als EIGENE Ausnahmetypen (<c>SidecarInsufficientVramException</c> mit den VRAM-Zahlen,
    /// <c>SidecarRequestTimeoutException</c> mit dem Zeitlimit, <c>TrainingExportPlanException</c>,
    /// <c>SchachtProArchiveException</c> u. a.). Ohne diese Erkennung wuerden die rund 85 in 10c2
    /// umgestellten Anzeigestellen genau diese fachliche Auskunft durch den generischen Satz
    /// ersetzen. Weiterhin gilt der EXAKTE Typ (keine Unterklassen der Framework-Typen wie
    /// <see cref="FileNotFoundException"/>) und der Wurfort in einer eigenen Schicht: dieselbe
    /// <see cref="IOException"/> aus <c>File.Copy</c> (Wurfort System.Private.CoreLib) bleibt beim
    /// generischen Satz. <c>SidecarBadRequestException</c> ist bewusst ausgenommen: Ihr
    /// Text traegt den rohen Antwortkoerper des Sidecars.
    /// </summary>
    private static bool IstEigenerMeldungstyp(Type typ)
    {
        if (typ == typeof(InvalidOperationException)
            || typ == typeof(ArgumentException)
            || typ == typeof(IOException)
            || typ == typeof(InvalidDataException)
            || typ == typeof(JsonException))
            return true;

        if (typ == typeof(AuswertungPro.Next.Application.Ai.SidecarBadRequestException))
            return false;

        // Die WebGIS-Ausnahmen (Sitzung, Serverantwort) bleiben beim bisherigen Verhalten: Ihre
        // Anzeige regeln die (geschuetzten) WebGIS-Ablaeufe selbst, und ihr Text kann eine rohe
        // Serverantwort tragen.
        if (typ.Namespace?.Contains(".WebGis", StringComparison.Ordinal) == true)
            return false;

        var deklariertIn = typ.Assembly.GetName().Name;
        return deklariertIn is not null
            && Array.IndexOf(EigeneProduktAssemblies, deklariertIn) >= 0;
    }
}
