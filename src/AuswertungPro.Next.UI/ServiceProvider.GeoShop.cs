using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IGeoShopLeser? _geoShop;
    public IGeoShopLeser GeoShop => _geoShop ??= new GeoShopXtfLeser();
    private IGeoShopSicherung? _geoShopSicherung;
    public IGeoShopSicherung GeoShopSicherung => _geoShopSicherung ??= new GeoShopSicherungsdatei(
        System.IO.Path.Combine(AppSettings.AppDataDir, "GeoShop-Sicherungen"));
    private Application.UseCases.Xtf.IXtfPaketAblage? _xtfPaketAblage;
    /// <summary>Legt das Katasterpaket an und packt es; siehe XtfKatasterPaketUseCase.</summary>
    public Application.UseCases.Xtf.IXtfPaketAblage XtfPaketAblage => _xtfPaketAblage ??=
        new Infrastructure.Import.Xtf.XtfPaketAblage();
    private Application.Xtf.Lieferung.IXtfLieferungsAblage? _xtfLieferungen;
    public Application.Xtf.Lieferung.IXtfLieferungsAblage XtfLieferungen => _xtfLieferungen ??=
        new Infrastructure.Import.Xtf.Lieferung.XtfLieferungsAblage();
}
