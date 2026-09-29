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
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var relevant = Unwrap(exception);

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

    public static string DescribeAndReport(Exception exception, string context)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var safeContext = string.IsNullOrWhiteSpace(context) ? "Vorgang" : context.Trim();
        BestEffort.ReportWarning($"[{safeContext}] {exception}");
        return Describe(exception);
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException { InnerExceptions.Count: 1 } aggregate)
            exception = aggregate.InnerExceptions[0];
        return exception;
    }
}
