using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI
{
    public sealed partial class ServiceProvider
    {
        private IKatasterKennungLeser? _katasterKennungen;

        /// <summary>
        /// Liest die Kennungstabelle fuer "Katasterkennungen ergaenzen": die
        /// SIA405-Kennungen, unter denen GEONIS jede Haltung und jeden Schacht
        /// fuehrt, aus einer GeoPackage-Kopie der GEONIS-Datenbank.
        ///
        /// Warum ein eigener Weg neben dem QGIS-Bestand: Die oeffentlichen
        /// QGIS-Kopien (WFS-Dienst der Lisag) tragen diese Kennung nicht — nur eine
        /// Lisag-Nummer, die bei jeder Veroeffentlichung neu vergeben wird. Ohne die
        /// GEONIS-Kennung legt ein Import in den Kataster Duplikate an, statt die
        /// Bauteile zu aktualisieren.
        ///
        /// Der Pfad wird bei JEDEM Lauf frisch aus den Einstellungen gelesen.
        /// </summary>
        public IKatasterKennungLeser KatasterKennungen =>
            _katasterKennungen ??= new KatasterKennungGpkgLeser(() => Settings.KatasterKennungenGpkgPath);
    }
}
