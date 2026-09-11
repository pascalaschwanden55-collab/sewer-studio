using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private IGeoShopLeser? _geoShop;
    public IGeoShopLeser GeoShop => _geoShop ??= new GeoShopXtfLeser();
}
