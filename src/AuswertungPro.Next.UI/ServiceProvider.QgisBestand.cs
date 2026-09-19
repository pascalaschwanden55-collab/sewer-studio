using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI
{
    public sealed partial class ServiceProvider
    {
        private IQgisBestandLeser? _qgisBestand;
        private ISchachtLageQuelle? _schachtLage;

        /// <summary>
        /// Schachtpunkt und Leitungsrichtungen fuer den Grundriss der Schachtgrafik, aus
        /// denselben QGIS-Kopien wie <see cref="QgisBestand"/>. Pfade bei jedem Lauf frisch aus
        /// den Einstellungen; nur lesend, nur fuer die Anzeige.
        /// </summary>
        public ISchachtLageQuelle SchachtLage =>
            _schachtLage ??= new QgisGpkgSchachtLageLeser(
                () => Settings.QgisSchaechteGpkgPath,
                () => Settings.QgisHaltungenGpkgPath);

        /// <summary>
        /// Liest die lokalen QGIS-Kopien des Abwassernetzes fuer
        /// "Leere Felder aus QGIS ergaenzen".
        ///
        /// Eine GeoPackage-Datei ist eine SQLite-Datenbank — der ganze Bestand
        /// laesst sich daraus offline lesen. Das ist der Grund fuer diesen Weg
        /// neben dem bestehenden Einzelnachschlag: Der Netzdienst des Kantons
        /// drosselt, ein Sammellauf ueber ein Projekt waere dort hunderte
        /// Einzelabfragen.
        ///
        /// Die Pfade werden bei JEDEM Lauf frisch aus den Einstellungen gelesen,
        /// damit eine Aenderung dort sofort greift und nicht erst nach einem
        /// Programmneustart.
        /// </summary>
        public IQgisBestandLeser QgisBestand =>
            _qgisBestand ??= new QgisGpkgBestandLeser(art => art == BauteilArt.Haltung
                ? Settings.QgisHaltungenGpkgPath
                : Settings.QgisSchaechteGpkgPath);
    }
}
