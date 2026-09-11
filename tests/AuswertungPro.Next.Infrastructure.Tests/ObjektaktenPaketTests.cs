using System.Text.Json;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ObjektaktenPaketTests
{
    [Fact]
    public void Zusatzrunde_erhaelt_Handleerwert_Fremdcode_Herkunft_und_zwei_Ereignisse()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        h.SetFieldValue(FieldKeys.Remarks, "", FieldSource.Manual, true);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        b.Neu("sanierung"); var e = b.Neu("sanierung");
        e.Werte["unbekannt"] = new() { Text = "Fremdtext", Originalcode = "Q-44", VonHand = true };
        e.Quellen.Add(new() { Kennung = "echte-tid", Werte = new() { ["Rohdatum"] = "20181231" } });
        var path = Path.Combine(Path.GetTempPath(), $"objektpaket-{Guid.NewGuid():N}.json");
        var service = new ObjektaktenPaketService();
        try
        {
            service.Exportiere(p, path);
            var paket = service.Lies(path);
            var ziel = new Project { Id = p.Id }; ziel.Data.Add(new HaltungRecord { Id = h.Id });
            ObjektaktenPaketImport.Uebernehme(ziel, paket);
            Assert.True(ziel.Data[0].FieldMeta[FieldKeys.Remarks].UserEdited);
            Assert.Equal(2, ziel.Objektakten.Count);
            Assert.Equal("Q-44", ziel.Objektakten.Single(a => a.Id == e.Id).Werte["unbekannt"].Originalcode);
            Assert.Equal("20181231", ziel.Objektakten.Single(a => a.Id == e.Id).Quellen[0].Werte["Rohdatum"]);
            ObjektaktenPaketImport.Uebernehme(ziel, paket);
            Assert.Equal(2, ziel.Objektakten.Count);
            Assert.Single(ziel.Objektakten.Single(a => a.Id == e.Id).Quellen);
            Assert.Throws<IOException>(() => service.Exportiere(ziel, path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Fremdes_Projekt_oder_defekte_Beziehung_lassen_Projekt_vollstaendig_unberuehrt()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var paket = new ObjektaktenPaket { ProjektId = p.Id,
            Haltungsfelder = new() { [h.Id] = new() { [FieldKeys.Remarks] = "nicht schreiben" } },
            Akten = [new() { Art = "deckel", Bezuege = [h.Id] }] };
        var vorher = JsonSerializer.Serialize(p);
        Assert.Throws<InvalidOperationException>(() => ObjektaktenPaketImport.Uebernehme(p, paket));
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
        paket.Akten.Clear(); paket.ProjektId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => ObjektaktenPaketImport.Uebernehme(p, paket));
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
    }

    [Fact]
    public void Zusatzwerte_ergaenzen_keine_Namen_und_ueberschreiben_keine_Handwerte()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        h.SetFieldValue(FieldKeys.Remarks, "", FieldSource.Manual, true);
        var paket = new ObjektaktenPaket { ProjektId = p.Id, Haltungsfelder = new()
        { [h.Id] = new() { [FieldKeys.HoldingName] = "Umbenannt", [FieldKeys.Remarks] = "Alt" } } };
        ObjektaktenPaketImport.Uebernehme(p, paket);
        Assert.Equal("", h.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal("", h.GetFieldValue(FieldKeys.Remarks));
    }
}
