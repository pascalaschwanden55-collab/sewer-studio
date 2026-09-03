using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Erstexport: Aus dem Projektstand entsteht eine vollstaendige neue SIA405-XTF fuer
/// Eigenstaendiger Voll-Export des Projektstands mit eigenen XTF-Kennungen.
/// </summary>
public sealed class XtfNeuPlanBuilderTests
{
    [Fact]
    public void Eine_Haltung_wird_zum_vollstaendigen_Objektverbund()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401"), Schacht("80409")]);

        Assert.Equal(1, plan.Haltungen);
        Assert.Equal(2, plan.Schaechte);

        // Kanal, Haltung, 2 Haltungspunkte, Rohrprofil, 2x(Normschacht+Abwasserknoten), Organisation
        Assert.Equal(
            new[] { "Abwasserknoten", "Haltung", "Haltungspunkt", "Kanal", "Normschacht",
                    "Organisation", "Rohrprofil" },
            plan.Objekte.Select(o => o.Klasse).Distinct().OrderBy(k => k, StringComparer.Ordinal));

        Assert.Equal(2, plan.Objekte.Count(o => o.Klasse == "Haltungspunkt"));
    }

    [Fact]
    public void Die_Haltung_verweist_auf_Kanal_Profil_und_beide_Haltungspunkte()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401"), Schacht("80409")]);
        var haltung = plan.Objekte.Single(o => o.Klasse == "Haltung");

        var namen = haltung.Verweise.Select(v => v.Name).ToList();
        Assert.Contains("AbwasserbauwerkRef", namen);
        Assert.Contains("RohrprofilRef", namen);
        Assert.Contains("vonHaltungspunktRef", namen);
        Assert.Contains("nachHaltungspunktRef", namen);

        var kanal = plan.Objekte.Single(o => o.Klasse == "Kanal");
        Assert.Equal(
            kanal.Tid,
            haltung.Verweise.Single(v => v.Name == "AbwasserbauwerkRef").ZielTid);
    }

    [Fact]
    public void Der_Haltungspunkt_haengt_am_Abwasserknoten_seines_Schachts()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401"), Schacht("80409")]);

        var oben = plan.Objekte.Single(o => o.Klasse == "Haltungspunkt"
            && o.Felder.Any(f => f.Value == "80401-80409_von"));
        var knoten = plan.Objekte.Single(o => o.Klasse == "Abwasserknoten"
            && o.Felder.Any(f => f.Value == "80401"));

        Assert.Equal(
            knoten.Tid,
            oben.Verweise.Single(v => v.Name == "AbwassernetzelementRef").ZielTid);
    }

    [Fact]
    public void Ohne_Eigentuemer_entsteht_kein_Objekt()
    {
        // EigentuemerRef ist am Abwasserbauwerk {1} — Pflicht. Eine erfundene
        // Organisation waere eine Aussage, die niemand getroffen hat.
        var record = Haltung();
        record.SetFieldValue(FieldKeys.Owner, "", FieldSource.Manual, userEdited: true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Equal(0, plan.Haltungen);
        Assert.Empty(plan.Objekte);
        Assert.Contains("Eigentuemer", Assert.Single(plan.Hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_unbekannter_Organisationstyp_sperrt_das_Objekt()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.Owner, "Firma Muster GmbH", FieldSource.Manual, userEdited: true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Equal(0, plan.Haltungen);
        Assert.Contains("Organisationstyp", Assert.Single(plan.Hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Organisation_traegt_den_Pflichtstatus_aktiv()
    {
        // Ohne "Status" weist der ilivalidator die ganze Datei ab.
        var organisation = XtfNeuPlanBuilder.Build([Haltung()], []).Objekte
            .Single(o => o.Klasse == "Organisation");

        Assert.Equal("aktiv", organisation.Felder.Single(f => f.Key == "Status").Value);
        Assert.True(organisation.ImTopicAdministration);
    }

    [Fact]
    public void Dieselben_Daten_ergeben_dieselben_Kennungen()
    {
        // Waeren die Kennungen zufaellig, legte das Zielsystem bei jedem Export neue
        // Objekte an — aus einer Korrektur wuerde eine Verdopplung.
        var eins = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401")], "PROJEKT-A");
        var zwei = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401")], "PROJEKT-A");

        Assert.Equal(
            eins.Objekte.Select(o => o.Tid),
            zwei.Objekte.Select(o => o.Tid));
    }

    [Fact]
    public void Ein_anderes_Projekt_ergibt_andere_Kennungen()
    {
        var a = XtfNeuPlanBuilder.Build([Haltung()], [], "PROJEKT-A");
        var b = XtfNeuPlanBuilder.Build([Haltung()], [], "PROJEKT-B");

        Assert.Empty(a.Objekte.Select(o => o.Tid).Intersect(b.Objekte.Select(o => o.Tid)));
    }

    [Fact]
    public void Jede_Kennung_ist_sechzehn_Zeichen_lang()
    {
        // STANDARDOID ist in INTERLIS OID TEXT*16. Fuenfzehn weist der Pruefer ab.
        var plan = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401")]);

        Assert.All(plan.Objekte, o =>
        {
            Assert.Equal(16, o.Tid.Length);
            Assert.True(char.IsLetter(o.Tid[0]));
            Assert.All(o.Tid, z => Assert.True(char.IsLetterOrDigit(z)));
        });
    }

    [Fact]
    public void Die_Haltungspunkte_erben_Anfang_und_Ende_des_Verlaufs()
    {
        var geometrien = new Dictionary<string, XtfNeuGeometrie>(StringComparer.OrdinalIgnoreCase)
        {
            ["80401-80409"] = new("Verlauf",
            [
                new XtfPunkt(2692610.782, 1192387.247),
                new XtfPunkt(2692609.662, 1192384.257),
                new XtfPunkt(2692606.892, 1192380.717)
            ])
        };

        var plan = XtfNeuPlanBuilder.Build([Haltung()], [], null, geometrien);

        var haltung = plan.Objekte.Single(o => o.Klasse == "Haltung");
        Assert.Equal(3, haltung.Geometrie!.Punkte.Count);

        var punkte = plan.Objekte.Where(o => o.Klasse == "Haltungspunkt").ToList();
        Assert.Equal(2692610.782, punkte[0].Geometrie!.Punkte[0].Ost);
        Assert.Equal(2692606.892, punkte[1].Geometrie!.Punkte[0].Ost);
    }

    [Fact]
    public void Ohne_Verlauf_entsteht_das_Objekt_trotzdem_und_der_Bericht_sagt_es()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], []);

        Assert.Equal(1, plan.Haltungen);
        Assert.Null(plan.Objekte.Single(o => o.Klasse == "Haltung").Geometrie);
        Assert.Contains(plan.Hinweise, h => h.Contains("Verlauf", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_zu_langer_Name_sperrt_die_Haltung()
    {
        // Abwasserbauwerk.Bezeichnung ist MANDATORY TEXT*41.
        var record = Haltung();
        record.SetFieldValue(FieldKeys.HoldingName, new string('a', 42), FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Equal(0, plan.Haltungen);
        Assert.Contains("42 Zeichen", Assert.Single(plan.Hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_leeres_Projekt_ergibt_einen_leeren_Plan()
    {
        var plan = XtfNeuPlanBuilder.Build([], []);

        Assert.True(plan.Leer);
        Assert.Empty(plan.Objekte);
    }

    [Fact]
    public void Mehrere_Haltungen_teilen_sich_ein_Rohrprofil_und_eine_Organisation()
    {
        var zweite = Haltung();
        zweite.SetFieldValue(FieldKeys.HoldingName, "80409-80538", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([Haltung(), zweite], []);

        Assert.Equal(2, plan.Haltungen);
        Assert.Single(plan.Objekte.Where(o => o.Klasse == "Rohrprofil"));
        Assert.Single(plan.Objekte.Where(o => o.Klasse == "Organisation"));
    }

    [Fact]
    public void Zwei_Haltungen_am_selben_Schacht_bekommen_verschiedene_Punktnamen()
    {
        // Haltungspunkt.Constraint1 verlangt, dass Bezeichnung und Datenherr zusammen
        // eindeutig sind. In einer Kette 1-2, 2-3 teilen sich beide Haltungen den
        // Schacht 2 — nach ihm benannt, wiese der ilivalidator die Datei ab.
        var zweite = Haltung();
        zweite.SetFieldValue(FieldKeys.HoldingName, "80409-80538", FieldSource.Manual, true);
        zweite.SetFieldValue("Schacht_oben", "80409", FieldSource.Manual, true);
        zweite.SetFieldValue("Schacht_unten", "80538", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([Haltung(), zweite], []);

        var namen = plan.Objekte
            .Where(o => o.Klasse == "Haltungspunkt")
            .Select(o => o.Felder.Single(f => f.Key == "Bezeichnung").Value)
            .ToList();

        Assert.Equal(4, namen.Count);
        Assert.Equal(4, namen.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Der_Punktname_bleibt_innerhalb_der_Laengengrenze()
    {
        // Haltungspunkt.Bezeichnung ist MANDATORY TEXT*20.
        var record = Haltung();
        record.SetFieldValue(FieldKeys.HoldingName, new string('h', 40), FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.All(
            plan.Objekte.Where(o => o.Klasse == "Haltungspunkt"),
            o => Assert.InRange(o.Felder.Single(f => f.Key == "Bezeichnung").Value.Length, 1, 20));
    }

    [Fact]
    public void Auch_gleiche_gekuerzte_Namen_bleiben_unterscheidbar()
    {
        // Zwei lange Namen, die sich erst nach dem 20. Zeichen unterscheiden.
        var a = Haltung();
        a.SetFieldValue(FieldKeys.HoldingName, new string('h', 30) + "-A", FieldSource.Manual, true);
        var b = Haltung();
        b.SetFieldValue(FieldKeys.HoldingName, new string('h', 30) + "-B", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([a, b], []);

        var namen = plan.Objekte
            .Where(o => o.Klasse == "Haltungspunkt")
            .Select(o => o.Felder.Single(f => f.Key == "Bezeichnung").Value)
            .ToList();

        Assert.Equal(4, namen.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(namen, n => Assert.InRange(n.Length, 1, 20));
    }

    [Fact]
    public void Ein_nicht_abbildbarer_Wert_verschwindet_nicht_still()
    {
        // "GFK" hat in SIA405 bewusst kein Gegenstueck — es ist nicht dasselbe wie
        // Kunststoff_Polyester_GUP. Ohne Hinweis fehlte das Material spurlos in der
        // Datei, obwohl es im Programm dasteht (real aufgefallen am Projekt "Test").
        var record = Haltung();
        record.SetFieldValue(FieldKeys.PipeMaterial, "GFK", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var haltung = plan.Objekte.Single(o => o.Klasse == "Haltung");
        Assert.DoesNotContain(haltung.Felder, f => f.Key == "Material");
        Assert.Contains(plan.Hinweise, h =>
            h.Contains("Material", StringComparison.Ordinal)
            && h.Contains("GFK", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_abbildbares_Material_geht_hinaus()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, true);

        var haltung = XtfNeuPlanBuilder.Build([record], []).Objekte
            .Single(o => o.Klasse == "Haltung");

        Assert.Equal("Steinzeug", haltung.Felder.Single(f => f.Key == "Material").Value);
    }

    [Fact]
    public void Ein_Schacht_mit_Umlaut_im_Feldnamen_geht_trotzdem_mit()
    {
        // Schachtfelder heissen nach der Kopfzeile der Excel-Vorlage. Der Eigentuemer
        // steht dort unter "Eigentümer" mit Umlaut, FieldKeys.Owner lautet aber
        // "Eigentuemer". Wer direkt danach greift, findet nichts — und weil der
        // Eigentuemer in SIA405 Pflicht ist, fiel dann JEDER Schacht aus dem Export.
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "78998", FieldSource.Manual, true);
        schacht.SetFieldValue("Eigentümer", "Privat", FieldSource.Manual, true);
        schacht.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([], [schacht]);

        Assert.Equal(1, plan.Schaechte);
        Assert.Single(plan.Objekte.Where(o => o.Klasse == "Normschacht"));
    }

    [Fact]
    public void Auch_Dimension_und_Zustand_werden_unter_jeder_Schreibweise_gefunden()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "78998", FieldSource.Manual, true);
        schacht.SetFieldValue("Eigentümer", "Privat", FieldSource.Manual, true);
        schacht.SetFieldValue("Dimension", "600 mm", FieldSource.Manual, true);
        schacht.SetFieldValue("Zustandsklasse", "3", FieldSource.Manual, true);

        var normschacht = XtfNeuPlanBuilder.Build([], [schacht]).Objekte
            .Single(o => o.Klasse == "Normschacht");

        Assert.Equal("600", normschacht.Felder.Single(f => f.Key == "Dimension1").Value);
        Assert.Equal("Z3", normschacht.Felder.Single(f => f.Key == "BaulicherZustand").Value);
    }

    // Im Projekt Jagdmatt stand an 46 Haltungen eine automatisch erzeugte Bemerkung mit
    // ueber 150 Zeichen. Der Bericht sagte dazu "hat in SIA405 keinen Wert" — als waere
    // der Text ein unbekannter Begriff. Tatsaechlich scheitert er nur an der Grenze
    // TEXT*80, und genau das muss der Bericht sagen, damit jemand kuerzen kann.
    [Fact]
    public void Eine_zu_lange_Bemerkung_wird_mit_ihrer_Zeichenzahl_gemeldet()
    {
        var lang = new string('x', 150);
        var record = Haltung();
        record.SetFieldValue(FieldKeys.Remarks, lang, FieldSource.Manual, true);
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.Remarks, lang, FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], [schacht]);

        Assert.DoesNotContain(plan.Hinweise, h => h.Contains("keinen Wert", StringComparison.Ordinal));
        Assert.Contains(plan.Hinweise, h =>
            h.StartsWith("80401-80409:", StringComparison.Ordinal)
            && h.Contains("150 Zeichen", StringComparison.Ordinal)
            && h.Contains("80", StringComparison.Ordinal));
        Assert.Contains(plan.Hinweise, h =>
            h.StartsWith("Schacht 80401:", StringComparison.Ordinal)
            && h.Contains("150 Zeichen", StringComparison.Ordinal));
    }

    // Die Haltung kennt in SIA405 nur die lichte Hoehe. Die Breite eines Rechteck- oder
    // Eiprofils steckt als Hoehen-Breiten-Verhaeltnis am Rohrprofil (1000/600 = 1.66667).
    [Fact]
    public void Zwei_verschiedene_Masse_werden_zum_Verhaeltnis_am_Rohrprofil()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.ProfileType, "Rechteckprofil", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var profil = Assert.Single(plan.Objekte, o => o.Klasse == "Rohrprofil");
        Assert.Equal("1.66667", profil.Felder.Single(f => f.Key == "HoehenBreitenverhaeltnis").Value);
        Assert.Equal("Rechteckprofil", profil.Felder.Single(f => f.Key == "Profiltyp").Value);
        Assert.Equal("1000", plan.Objekte.Single(o => o.Klasse == "Haltung").Felder.Single(f => f.Key == "Lichte_Hoehe").Value);
        Assert.DoesNotContain(plan.Hinweise, h => h.Contains("Verhaeltnis", StringComparison.Ordinal));
    }

    [Fact]
    public void Rund_ergibt_kein_Verhaeltnis()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "300", FieldSource.Manual, true);

        var profil = Assert.Single(XtfNeuPlanBuilder.Build([record], []).Objekte, o => o.Klasse == "Rohrprofil");

        Assert.DoesNotContain(profil.Felder, f => f.Key == "HoehenBreitenverhaeltnis");
    }

    [Fact]
    public void Zwei_Masse_am_Kreisprofil_werden_gemeldet_statt_geschrieben()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var profil = Assert.Single(plan.Objekte, o => o.Klasse == "Rohrprofil");
        Assert.DoesNotContain(profil.Felder, f => f.Key == "HoehenBreitenverhaeltnis");
        Assert.Contains(plan.Hinweise, h => h.Contains("Kreisprofil", StringComparison.Ordinal)
                                            && h.Contains("1000 x 600", StringComparison.Ordinal));
    }

    [Fact]
    public void Zwei_Masse_ohne_Profiltyp_werden_gemeldet()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.ProfileType, "", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "900", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.DoesNotContain(plan.Objekte, o => o.Klasse == "Rohrprofil");
        Assert.Contains(plan.Hinweise, h => h.Contains("kein Profiltyp", StringComparison.Ordinal));
    }

    [Fact]
    public void Verschiedene_Verhaeltnisse_bekommen_verschiedene_Rohrprofile()
    {
        var eins = Haltung();
        eins.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        eins.SetFieldValue(FieldKeys.NominalDiameterMm, "900", FieldSource.Manual, true);
        eins.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, true);
        var zwei = Haltung();
        zwei.SetFieldValue(FieldKeys.HoldingName, "80409-80538", FieldSource.Manual, true);
        zwei.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        zwei.SetFieldValue(FieldKeys.NominalDiameterMm, "1200", FieldSource.Manual, true);
        zwei.SetFieldValue(FieldKeys.ClearWidthMm, "800", FieldSource.Manual, true);

        var profile = XtfNeuPlanBuilder.Build([eins, zwei], []).Objekte.Where(o => o.Klasse == "Rohrprofil").ToList();

        // 900/600 und 1200/800 sind dasselbe Verhaeltnis 1.5: ein gemeinsames Profil.
        var profil = Assert.Single(profile);
        Assert.Equal("1.5", profil.Felder.Single(f => f.Key == "HoehenBreitenverhaeltnis").Value);
        Assert.NotEqual("Eiprofil", profil.Felder.Single(f => f.Key == "Bezeichnung").Value);
    }

    [Fact]
    public void Eine_Haltung_mit_Qgis_Objekt_ID_wird_mit_eigener_Xtf_Kennung_exportiert()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Equal(1, plan.Haltungen);
        var haltung = Assert.Single(plan.Objekte, o => o.Klasse == "Haltung");
        Assert.StartsWith("chSST", haltung.Tid, StringComparison.Ordinal);
        Assert.DoesNotContain("866789", haltung.Tid, StringComparison.Ordinal);
        Assert.Contains(plan.Hinweise, h => h.Contains("866789", StringComparison.Ordinal)
                                            && h.Contains("Revidierte XTF", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Schacht_mit_Qgis_Objekt_ID_wird_mit_eigener_Xtf_Kennung_exportiert()
    {
        var record = Schacht("78998");
        record.SetFieldValue(FieldKeys.CadastreObjectId, "768645", FieldSource.Kataster, false);

        var plan = XtfNeuPlanBuilder.Build([], [record]);

        Assert.Equal(1, plan.Schaechte);
        var schacht = Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht");
        Assert.StartsWith("chSST", schacht.Tid, StringComparison.Ordinal);
        Assert.Contains(plan.Hinweise, h => h.Contains("768645", StringComparison.Ordinal)
                                            && h.Contains("Revidierte XTF", StringComparison.Ordinal));
    }

    [Fact]
    public void Seilergasse_mit_Haltungs_Objekt_ID_erzeugt_Haltung_und_Schacht()
    {
        var haltung = Haltung();
        haltung.SetFieldValue(FieldKeys.HoldingName, "78998-79002", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_oben", "78998", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_unten", "79002", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);

        var plan = XtfNeuPlanBuilder.Build([haltung], [Schacht("78998")]);

        Assert.Equal(1, plan.Haltungen);
        Assert.Equal(1, plan.Schaechte);
        Assert.Single(plan.Objekte, o => o.Klasse == "Haltung");
        Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht");
        var knoten = Assert.Single(plan.Objekte, o => o.Klasse == "Abwasserknoten");
        Assert.Contains(plan.Objekte.Where(o => o.Klasse == "Haltungspunkt")
            .SelectMany(o => o.Verweise), v =>
                v.Name == "AbwassernetzelementRef" && v.ZielTid == knoten.Tid);
    }

    [Fact]
    public void Ein_Schacht_an_einer_neuen_Haltung_bleibt_exportierbar()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], [Schacht("80401")]);

        Assert.Equal(1, plan.Haltungen);
        Assert.Equal(1, plan.Schaechte);
        Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht");
    }

    // Punkt 2 derselben Analyse: Eigentuemer "Privat", Datenherr und Datenlieferant
    // "Abwasser Uri" — die Datei setzte alle drei auf "Privat".
    [Fact]
    public void Datenherr_und_Datenlieferant_kommen_aus_den_Feldern_nicht_vom_Eigentuemer()
    {
        var record = Haltung();
        record.SetFieldValue(FieldKeys.DataOwner, "Abwasser Uri", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.DataSupplier, "Abwasser Uri", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var kanal = plan.Objekte.Single(o => o.Klasse == "Kanal");
        var organisationen = plan.Objekte.Where(o => o.Klasse == "Organisation").ToList();
        Assert.Equal(2, organisationen.Count);
        var privat = organisationen.Single(o => o.Felder.Any(f => f.Value == "Privat"));
        var awu = organisationen.Single(o => o.Felder.Any(f => f.Value == "Abwasser Uri"));
        Assert.Equal(privat.Tid, kanal.Verweise.Single(v => v.Name == "EigentuemerRef").ZielTid);
        Assert.Equal(awu.Tid, kanal.Verweise.Single(v => v.Name == "DatenherrRef").ZielTid);
        Assert.Equal(awu.Tid, kanal.Verweise.Single(v => v.Name == "DatenlieferantRef").ZielTid);
    }

    [Fact]
    public void Ohne_Datenherr_traegt_der_Eigentuemer_die_Verwaltung()
    {
        var plan = XtfNeuPlanBuilder.Build([Haltung()], []);

        var kanal = plan.Objekte.Single(o => o.Klasse == "Kanal");
        var eigentuemer = kanal.Verweise.Single(v => v.Name == "EigentuemerRef").ZielTid;
        Assert.Equal(eigentuemer, kanal.Verweise.Single(v => v.Name == "DatenherrRef").ZielTid);
        Assert.Single(plan.Objekte.Where(o => o.Klasse == "Organisation"));
    }

    [Theory]
    [InlineData(FieldKeys.DataOwner, "Datenherr")]
    [InlineData(FieldKeys.DataSupplier, "Datenlieferant")]
    public void Ein_gesetzter_unbekannter_Verwaltungswert_sperrt_die_Haltung(
        string feld, string rolle)
    {
        var record = Haltung();
        record.SetFieldValue(feld, "Firma Muster GmbH", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([record], []);

        Assert.Equal(0, plan.Haltungen);
        Assert.Empty(plan.Objekte);
        Assert.Contains(plan.Hinweise, h => h.Contains(rolle, StringComparison.Ordinal)
                                            && h.Contains("Firma Muster GmbH", StringComparison.Ordinal)
                                            && h.Contains("Organisationstyp", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_gesetzter_unbekannter_Datenherr_sperrt_den_Schacht()
    {
        var record = Schacht("78998");
        record.SetFieldValue(FieldKeys.DataOwner, "Firma Muster GmbH", FieldSource.Manual, true);

        var plan = XtfNeuPlanBuilder.Build([], [record]);

        Assert.Equal(0, plan.Schaechte);
        Assert.Empty(plan.Objekte);
        Assert.Contains(plan.Hinweise, h => h.Contains("Datenherr", StringComparison.Ordinal)
                                            && h.Contains("Firma Muster GmbH", StringComparison.Ordinal));
    }

    private static HaltungRecord Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "80401-80409", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, true);
        record.SetFieldValue("Schacht_oben", "80401", FieldSource.Manual, true);
        record.SetFieldValue("Schacht_unten", "80409", FieldSource.Manual, true);
        return record;
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        return record;
    }
}
