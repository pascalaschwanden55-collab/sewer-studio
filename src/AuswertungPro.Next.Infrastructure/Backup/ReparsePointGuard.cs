using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Erkennt Verknuepfungen/Junctions (Reparse Points) in Pfaden. Beim Spiegeln und
/// bei der Verwaisten-Loeschung duerfen solche Eintraege nie betreten werden: der
/// dahinterliegende Inhalt liegt ausserhalb des eigenen Baums und wuerde sonst
/// kopiert oder geloescht.
/// </summary>
internal static class ReparsePointGuard
{
    /// <summary>true, wenn der Eintrag selbst eine Verknuepfung/Junction ist (nicht lesbar = false).</summary>
    public static bool IsReparsePoint(string path)
        => VerknuepfungsSchutz.PruefeEintrag(path, VerknuepfungsRegel.Spiegel).Befund == VerknuepfungsBefund.Verknuepfung;

    /// <summary>
    /// true, wenn <paramref name="path"/> selbst oder ein Elternordner zwischen ihm und
    /// <paramref name="root"/> (exklusive) eine Verknuepfung/Junction ist. Der Root
    /// selbst wird bewusst nicht geprueft (er wurde bereits validiert). Gemeinsame Regel
    /// <see cref="VerknuepfungsRegel.Spiegel"/> (Deepscan A5): Lesefehler gelten als frei.
    /// </summary>
    public static bool HasReparsePointBelow(string root, string path)
        => VerknuepfungsSchutz.PruefeKette(root, path, VerknuepfungsRegel.Spiegel).Befund == VerknuepfungsBefund.Verknuepfung;
}
