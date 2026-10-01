using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Die EINE Regel, welche Nummer ein Schacht traegt. Auswahlfenster, Tabelle und
/// Nachfuehren muessen sie gemeinsam verwenden: laege dieselbe Suche mehrfach im
/// Code, koennte man einen Schacht waehlen, den die Tabelle danach nicht wiederfindet.
/// </summary>
public sealed class DossierShaftNumberPolicyTests
{
    [Fact]
    public void Die_Schachtnummer_gewinnt()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", " 80551 ");
        schacht.SetFieldValue("Nr.", "7");

        Assert.Equal("80551", DossierShaftNumberPolicy.NumberOf(schacht));
    }

    [Theory]
    [InlineData("Nr.")]
    [InlineData("NR.")]
    public void Ohne_Schachtnummer_gilt_die_laufende_Nummer(string feld)
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue(feld, "12");

        Assert.Equal("12", DossierShaftNumberPolicy.NumberOf(schacht));
    }

    [Fact]
    public void Ein_Schacht_ganz_ohne_Nummer_bleibt_leer()
    {
        // Ohne Nummer laesst er sich nicht speichern — er darf gar nicht erst
        // zur Auswahl stehen.
        Assert.Equal("", DossierShaftNumberPolicy.NumberOf(new SchachtRecord()));
    }

    [Fact]
    public void Die_Projektnummern_kommen_ohne_Leere_und_ohne_Doppelte()
    {
        var project = new Project();

        foreach (var nummer in new[] { "80551", " 80551 ", "", "36051" })
        {
            var schacht = new SchachtRecord();
            if (nummer.Length > 0)
                schacht.SetFieldValue("Schachtnummer", nummer);
            project.SchaechteData.Add(schacht);
        }

        var nummern = DossierShaftNumberPolicy.NumbersOf(project).ToList();

        Assert.Equal(new[] { "80551", "36051" }, nummern);
    }
}
