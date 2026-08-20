using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>Lesen und Schreiben der gelernten Faelle.</summary>
public interface IKostenfallStore
{
    /// <summary>Alle Faelle. Fehlende Datei = leer; beschaedigte Datei = Ausnahme.</summary>
    IReadOnlyList<Kostenfall> Lade();

    /// <summary>Ersetzt den Bestand vollstaendig und atomar.</summary>
    void Speichere(IReadOnlyList<Kostenfall> faelle);
}
