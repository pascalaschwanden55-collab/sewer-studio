using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI
{
    public sealed partial class ServiceProvider
    {
        private FeldNachschlagUseCase? _feldNachschlag;

        /// <summary>
        /// Schlaegt leere Schachtfelder beim Kanton nach (Kataster, spaeter
        /// auch Grundbuch). Schreibt selbst nichts — die Uebernahme bleibt
        /// eine bewusste Entscheidung des Bearbeiters.
        ///
        /// Bewusst bedarfsgesteuert: Beim ersten Nachschlag wird die
        /// Kataster-Tabelle aus einer mehrere hundert Megabyte grossen
        /// XTF-Datei aufgebaut. Das darf den Programmstart nicht belasten.
        /// </summary>
        public FeldNachschlagUseCase FeldNachschlag =>
            _feldNachschlag ??= FeldNachschlagComposition.Erzeuge(
                Mapping.KatasterXtfPathResolver.Resolve(Settings));
    }
}
