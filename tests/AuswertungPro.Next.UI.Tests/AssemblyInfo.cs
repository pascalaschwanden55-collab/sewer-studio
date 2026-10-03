// Parallele Testausfuehrung abschalten: mehrere Tests teilen den statischen VsaCodeResolver-Katalog,
// Umgebungsvariablen, temporaere SQLite-Verbindungen und WPF-Kindprozesse.
// Bewusst hier und nicht in einer Testdatei, damit der Schalter nicht versehentlich verloren geht.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
