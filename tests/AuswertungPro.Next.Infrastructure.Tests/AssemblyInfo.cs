// Parallele Testausfuehrung abschalten, da mehrere Tests den statischen VsaCodeResolver-Katalog
// und temporaere SQLite-Verbindungen teilen. In Produktion ist VsaCodeResolver ein
// einmal-konfiguriertes Singleton — die Isolation ist nur im Test noetig.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
