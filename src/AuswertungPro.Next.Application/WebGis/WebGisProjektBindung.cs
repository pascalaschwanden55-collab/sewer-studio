using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.WebGis;

/// <summary>
/// Hält fest, für welches Projekt eine WebGIS-Vorschau oder ein Schreiblauf gilt: dieselbe
/// Projektinstanz UND derselbe Speicherpfad. Nach jedem Warten auf das Netz wird damit geprüft,
/// ob noch genau dieses Projekt offen ist — eine verspätete Antwort für Projekt A darf nie zu
/// Projekt B gehören (Plan WG04, Prüfung 28.09.2026).
/// </summary>
public sealed class WebGisProjektBindung
{
    public WebGisProjektBindung(Project projekt, string? pfad)
    {
        Projekt = projekt ?? throw new ArgumentNullException(nameof(projekt));
        Pfad = pfad;
    }

    public Project Projekt { get; }
    public string? Pfad { get; }

    public bool Gilt(Project? aktuellesProjekt, string? aktuellerPfad)
        => ReferenceEquals(aktuellesProjekt, Projekt)
           && string.Equals(Normal(Pfad), Normal(aktuellerPfad), StringComparison.OrdinalIgnoreCase);

    private static string Normal(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return string.Empty;
        try
        {
            return Path.GetFullPath(pfad.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return pfad.Trim();
        }
    }
}
