using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI
{
    public sealed partial class ServiceProvider
    {
        private FeldNachschlagUseCase? _feldNachschlag;

        /// <summary>
        /// Schlaegt leere Schachtfelder beim Kanton nach: Funktion und Material
        /// im Abwasserkataster, Eigentuemer und Strasse im Grundbuch. Schreibt
        /// selbst nichts — die Uebernahme bleibt eine bewusste Entscheidung
        /// des Bearbeiters.
        ///
        /// Bewusst bedarfsgesteuert: Beim ersten Nachschlag wird die
        /// Kataster-Tabelle aus einer mehrere hundert Megabyte grossen
        /// XTF-Datei aufgebaut. Das darf den Programmstart nicht belasten.
        ///
        /// Der Grundbuchweg teilt sich das Tor nach draussen mit dem
        /// Eigentuemerdossier — ein Zeitlimit, Aufrufe der Reihe nach.
        /// </summary>
        public FeldNachschlagUseCase FeldNachschlag =>
            _feldNachschlag ??= FeldNachschlagComposition.Erzeuge(
                Mapping.KatasterXtfPathResolver.Resolve(Settings),
                DossierParcels,
                DossierLandRegistry,
                DossierSewerNetwork,
                meldung => Logger.LogInformation("Feldnachschlag: {Meldung}", meldung));
    }
}
