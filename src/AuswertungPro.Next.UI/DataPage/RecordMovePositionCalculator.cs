namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Reine Ziel-Index-Berechnung fuer "Verschieben auf Position" (1-basiert, geklemmt).
/// Bewusst ohne Bezug auf einen konkreten Record-Typ, damit Haltungs- und Schacht-
/// Ansicht dieselbe Logik teilen und sie fokussiert getestet werden kann.
/// </summary>
public static class RecordMovePositionCalculator
{
    /// <summary>
    /// Rechnet eine 1-basierte Zielposition in einen gueltigen 0-basierten Zielindex um.
    /// Zu kleine/grosse Positionen werden auf den ersten bzw. letzten Eintrag geklemmt.
    /// Liefert false, wenn kein sinnvoller Zug moeglich ist (leere Liste, ungueltiger
    /// Startindex oder Ziel == Start).
    /// </summary>
    public static bool TryResolveTargetIndex(int oldIndex, int count, int targetPosition, out int targetIndex)
    {
        targetIndex = -1;
        if (oldIndex < 0 || oldIndex >= count || count <= 0)
            return false;

        var resolved = targetPosition - 1;
        if (resolved < 0)
            resolved = 0;
        if (resolved >= count)
            resolved = count - 1;

        if (resolved == oldIndex)
            return false;

        targetIndex = resolved;
        return true;
    }
}
