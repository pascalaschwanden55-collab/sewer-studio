using AuswertungPro.Next.Application.Devis;
using AuswertungPro.Next.Domain.Models.Devis;

namespace AuswertungPro.Next.Infrastructure.Devis;

public sealed class DevisGenerator : IDevisGenerator
{
    private readonly IDevisMappingService _mappingService;

    public DevisGenerator(IDevisMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public DevisErgebnis Generate(string baustelle, string zone, List<HaltungMitSchaeden> haltungen)
    {
        var baumeister = new Eigendevis
        {
            Baustelle = baustelle,
            Zone = zone,
            Gewerk = GewerkTyp.Baumeister
        };

        var rohrleitungsbau = new Eigendevis
        {
            Baustelle = baustelle,
            Zone = zone,
            Gewerk = GewerkTyp.Rohrleitungsbau
        };

        var warnungen = new List<string>();
        var haltungMassnahmen = new List<(HaltungMitSchaeden Haltung, MassnahmenEmpfehlung Empfehlung)>();

        // 1. Determine best measure per section
        foreach (var haltung in haltungen)
        {
            var best = BestimmeMassnahme(haltung, warnungen);
            if (best.Mapping is not null)
                haltungMassnahmen.Add((haltung, best));
        }

        // 2. Aggregate into Hauptgruppen per Gewerk
        AggregiereBaumeister(baumeister, haltungMassnahmen);
        AggregiereRohrleitungsbau(rohrleitungsbau, haltungMassnahmen);

        // 3. Add standard positions (Installationen, Regie)
        ErgaenzeStandardPositionen(baumeister, haltungMassnahmen);
        ErgaenzeStandardPositionen(rohrleitungsbau, haltungMassnahmen);

        return new DevisErgebnis
        {
            Baumeister = baumeister,
            Rohrleitungsbau = rohrleitungsbau,
            Warnungen = warnungen
        };
    }

    private MassnahmenEmpfehlung BestimmeMassnahme(HaltungMitSchaeden haltung, List<string> warnungen)
    {
        var empfehlungen = haltung.Schaeden
            .Select(s => _mappingService.GetEmpfehlung(s.Code, s.Char1, s.Char2, s.Zustandsklasse, haltung.DN))
            .Where(e => e.Mapping is not null)
            .OrderByDescending(e => e.Mapping!.Prioritaet)
            .ToList();

        if (empfehlungen.Count == 0)
        {
            if (haltung.Schaeden.Count > 0)
                warnungen.Add($"Haltung {haltung.VonSchacht}-{haltung.BisSchacht}: Kein Mapping fuer Codes {string.Join(", ", haltung.Schaeden.Select(s => s.Code))}");
            return MassnahmenEmpfehlung.KeineEmpfehlung("");
        }

        return empfehlungen[0];
    }

    private void AggregiereBaumeister(
        Eigendevis devis,
        List<(HaltungMitSchaeden Haltung, MassnahmenEmpfehlung Empfehlung)> massnahmen)
    {
        // Group Baumeister positions by Hauptposition
        var gruppen = new Dictionary<int, DevisHauptgruppe>();

        foreach (var (haltung, empfehlung) in massnahmen)
        {
            var mapping = empfehlung.Mapping!;
            if (mapping.BaumeisterPositionen.Count == 0) continue;

            // Group positions that belong to an Abschnitt (e.g., Bauarbeiten per Haltung)
            var abschnittGruppen = mapping.BaumeisterPositionen
                .GroupBy(p => p.Hauptposition);

            foreach (var pg in abschnittGruppen)
            {
                if (!gruppen.TryGetValue(pg.Key, out var hg))
                {
                    hg = new DevisHauptgruppe
                    {
                        Nummer = pg.Key,
                        Bezeichnung = GetGruppenBezeichnung(pg.Key)
                    };
                    gruppen[pg.Key] = hg;
                }

                var abschnitt = new DevisAbschnitt
                {
                    Bezeichnung = $"Abschnitt {haltung.VonSchacht} - {haltung.BisSchacht}",
                    VonSchacht = haltung.VonSchacht,
                    BisSchacht = haltung.BisSchacht
                };

                foreach (var vorlage in pg)
                {
                    var menge = BerechneMenge(vorlage.MengenFormel, haltung);
                    abschnitt.Positionen.Add(CreatePosition(vorlage, menge, haltung, empfehlung));
                }

                hg.Abschnitte.Add(abschnitt);
            }
        }

        devis.Hauptgruppen = gruppen.OrderBy(g => g.Key).Select(g => g.Value).ToList();
    }

    private void AggregiereRohrleitungsbau(
        Eigendevis devis,
        List<(HaltungMitSchaeden Haltung, MassnahmenEmpfehlung Empfehlung)> massnahmen)
    {
        var gruppen = new Dictionary<int, DevisHauptgruppe>();

        foreach (var (haltung, empfehlung) in massnahmen)
        {
            var mapping = empfehlung.Mapping!;
            if (mapping.RohrleitungsbauPositionen.Count == 0) continue;

            var abschnittGruppen = mapping.RohrleitungsbauPositionen
                .GroupBy(p => p.Hauptposition);

            foreach (var pg in abschnittGruppen)
            {
                if (!gruppen.TryGetValue(pg.Key, out var hg))
                {
                    hg = new DevisHauptgruppe
                    {
                        Nummer = pg.Key,
                        Bezeichnung = GetGruppenBezeichnung(pg.Key)
                    };
                    gruppen[pg.Key] = hg;
                }

                var abschnitt = new DevisAbschnitt
                {
                    Bezeichnung = $"Abschnitt {haltung.VonSchacht} - {haltung.BisSchacht}",
                    VonSchacht = haltung.VonSchacht,
                    BisSchacht = haltung.BisSchacht
                };

                foreach (var vorlage in pg)
                {
                    var menge = BerechneMenge(vorlage.MengenFormel, haltung);
                    abschnitt.Positionen.Add(CreatePosition(vorlage, menge, haltung, empfehlung));
                }

                hg.Abschnitte.Add(abschnitt);
            }
        }

        devis.Hauptgruppen = gruppen.OrderBy(g => g.Key).Select(g => g.Value).ToList();
    }

    private void ErgaenzeStandardPositionen(
        Eigendevis devis,
        List<(HaltungMitSchaeden Haltung, MassnahmenEmpfehlung Empfehlung)> massnahmen)
    {
        if (devis.Hauptgruppen.Count == 0) return;

        // Installationen (Hauptgruppe 1)
        var installation = new DevisHauptgruppe
        {
            Nummer = 1,
            Bezeichnung = "Installationen"
        };
        installation.Positionen.Add(new DevisPosition
        {
            Hauptposition = 1,
            Unterposition = 100,
            Einzelposition = 1,
            PositionNummer = "1.100.1",
            Bezeichnung = "Baustelleneinrichtung",
            Einheit = "Gl",
            Menge = 1,
            Einheitspreis = 5000,
            Herleitung = new MengenHerleitung
            {
                Quelle = "Standard",
                Formel = "1 (pauschal)",
                Konfidenz = ConfidenceLevel.High
            }
        });
        installation.Positionen.Add(new DevisPosition
        {
            Hauptposition = 1,
            Unterposition = 200,
            Einzelposition = 1,
            PositionNummer = "1.200.1",
            Bezeichnung = "Einrichtung Belagsarbeiten",
            Einheit = "Gl",
            Menge = 1,
            Einheitspreis = 500,
            Herleitung = new MengenHerleitung
            {
                Quelle = "Standard",
                Formel = "1 (pauschal)",
                Konfidenz = ConfidenceLevel.High
            }
        });

        devis.Hauptgruppen.Insert(0, installation);
    }

    private static DevisPosition CreatePosition(
        DevisPositionVorlage vorlage, decimal menge,
        HaltungMitSchaeden haltung, MassnahmenEmpfehlung empfehlung)
    {
        return new DevisPosition
        {
            Hauptposition = vorlage.Hauptposition,
            Unterposition = vorlage.Unterposition,
            Einzelposition = vorlage.Einzelposition,
            PositionNummer = $"{vorlage.Hauptposition}.{vorlage.Unterposition:0}.{vorlage.Einzelposition:0}",
            Bezeichnung = vorlage.Bezeichnung,
            Einheit = vorlage.Einheit,
            Menge = menge,
            Einheitspreis = vorlage.Referenzpreis,
            Herleitung = new MengenHerleitung
            {
                Quelle = "AI-Erkennung",
                Formel = vorlage.MengenFormel,
                BezogeneHaltungen = [haltung.HaltungsId],
                BezogeneSchaeden = haltung.Schaeden.Select(s => s.Code).ToList(),
                Konfidenz = empfehlung.Konfidenz
            }
        };
    }

    private static decimal BerechneMenge(string formel, HaltungMitSchaeden haltung)
    {
        var variablen = new Dictionary<string, decimal>
        {
            ["Haltungslaenge"] = haltung.Laenge,
            ["Grabentiefe"] = haltung.Grabentiefe,
            ["AnzahlSchaechte"] = haltung.AnzahlSchaechte,
            ["DN"] = haltung.DN
        };

        try
        {
            var result = FormelEvaluator.Evaluate(formel, variablen);
            return Math.Max(0, Math.Round(result, 2));
        }
        catch
        {
            return 1;
        }
    }

    private static string GetGruppenBezeichnung(int nummer) => nummer switch
    {
        1 => "Installationen",
        2 => "Abbrueche",
        3 => "Bauarbeiten fuer Werkleitungen",
        4 => "Transporte",
        5 => "Sanierungsarbeiten",
        6 => "Schaechte und Bauwerke",
        7 => "Belagsarbeiten",
        8 => "Regie",
        _ when nummer >= 400 && nummer < 500 => "Rohrleitungsbau",
        _ when nummer >= 500 && nummer < 600 => "Sanierungsverfahren",
        _ => $"Gruppe {nummer}"
    };
}
