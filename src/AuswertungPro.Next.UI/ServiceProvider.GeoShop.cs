using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IGeoShopLeser? _geoShop;
    public IGeoShopLeser GeoShop => _geoShop ??= new GeoShopXtfLeser();
    private Application.Xtf.Lieferung.IXtfLieferungsAblage? _xtfLieferungen;
    public Application.Xtf.Lieferung.IXtfLieferungsAblage XtfLieferungen => _xtfLieferungen ??=
        new Infrastructure.Import.Xtf.Lieferung.XtfLieferungsAblage();
}
