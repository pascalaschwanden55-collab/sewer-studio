namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Quelle eines Feldwerts. Priorität (hoch → niedrig): Manual > Xtf/Xtf405 > Ili > Pdf > Legacy > Unknown.
/// </summary>
public enum FieldSource
{
    Unknown = 0,
    Legacy = 1,
    Xtf = 3,
    Xtf405 = 5,
    Ili = 6,
    Pdf = 7,
    Manual = 10
}





