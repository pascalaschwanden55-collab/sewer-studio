using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Ziel einer Ablage: Liegt im Ordner schon eine bytegleiche Datei derselben Art, wird sie
/// wiederverwendet; sonst ein freier Name. Ohne diese Regel legte jede Wiederholung (nach einem
/// Teilfehler oder einfach ein zweiter Lauf) eine weitere Kopie mit «_01» an.
/// </summary>
internal static class DistributionTargetReuse
{
    internal readonly record struct Ziel(string Pfad, bool SchonVorhanden);

    internal static Ziel Bestimme(
        DistributionWritePathGuard writePaths,
        string ordner,
        string wunschPfad,
        string quelle,
        bool overwrite)
    {
        var endung = Path.GetExtension(wunschPfad);
        var vorhanden = Distributor.FindExistingIdenticalFile(
            ordner,
            quelle,
            pfad => string.Equals(Path.GetExtension(pfad), endung, StringComparison.OrdinalIgnoreCase));
        return vorhanden is not null
            ? new Ziel(writePaths.EnsureFileTarget(vorhanden), SchonVorhanden: true)
            : new Ziel(writePaths.ResolveUniqueFileTarget(wunschPfad, overwrite), SchonVorhanden: false);
    }

    /// <summary>Wie <see cref="Bestimme"/>, dazu Kopieren bzw. Verschieben, wenn die Datei noch fehlt.</summary>
    internal static Ziel Lege(
        DistributionWritePathGuard writePaths,
        string ordner,
        string wunschPfad,
        string quelle,
        bool moveInsteadOfCopy,
        bool overwrite)
    {
        var ziel = Bestimme(writePaths, ordner, wunschPfad, quelle, overwrite);
        if (!ziel.SchonVorhanden)
            DistributionFileTransfer.MoveOrCopy(quelle, ziel.Pfad, moveInsteadOfCopy, overwrite);
        return ziel;
    }
}
