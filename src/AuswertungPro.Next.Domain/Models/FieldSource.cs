namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Quelle eines Feldwerts. Priorität (hoch → niedrig):
/// Manual > Xtf/Xtf405 > Ili > Pdf/Spro > Legacy > Protocol > Unknown.
///
/// Die Zahlen sind nur Speicherwerte und bilden diese Reihenfolge NICHT ab
/// (Pdf = 7 steht ueber Xtf405 = 5). Massgebend ist allein die Rangtabelle in
/// MergeEngine.GetPriority.
/// </summary>
public enum FieldSource
{
    Unknown = 0,
    Legacy = 1,
    Protocol = 2,
    Xtf = 3,
    Xtf405 = 5,
    Ili = 6,
    Pdf = 7,

    /// <summary>SchachtPro-Archiv (.spro). Wie <see cref="Pdf"/> ein Protokollimport,
    /// aber eine eigene Quelle - sonst waere spaeter nicht unterscheidbar, woher ein
    /// Schachtwert stammt.</summary>
    Spro = 8,
    Manual = 10
}





